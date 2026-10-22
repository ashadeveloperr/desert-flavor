using System;
using DesertSettlement.Core.Config;
using DesertSettlement.Core.Economy;
using DesertSettlement.Core.Hex;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DesertSettlement.Runtime
{
    /// <summary>도구 패널에서 고르는 작업 모드.</summary>
    public enum BuildTool
    {
        Select,
        Well,
        Farm,
        Quarry,
        Mine,
        AuctionHall,
        Warehouse,
        Canal,
        Gate,
        Upgrade,
        Demolish,
        ToggleClose,
    }

    /// <summary>
    /// 마우스로 맵을 가리키고 도구를 적용한다(C7 임시 조작).
    /// - 시설 도구: 가리킨 타일에 건설 예약
    /// - 수로 도구: 마우스에 가장 가까운 변을 강조, 클릭 또는 드래그로 연속 설치 / 수문 도구: 가리킨 변
    /// - 개량·철거·폐쇄: 가리킨 시설(변 가까이면 수로·수문, 아니면 타일 시설)
    /// - 선택: 가리킨 시설을 HUD에 표시. 우클릭·Esc = 선택 도구로 돌아감
    /// 가능하면 강조색 초록, 불가하면 빨강 + 이유(<see cref="HoverMessage"/>).
    /// </summary>
    [RequireComponent(typeof(GameSession))]
    public sealed class BuildInputController : MonoBehaviour
    {
        [SerializeField] private Color validColor = new Color(0.3f, 1f, 0.4f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.25f);
        [SerializeField] private Color selectColor = new Color(1f, 0.95f, 0.4f);
        [Tooltip("변까지 거리가 타일 크기 × 이 값보다 가까우면 개량·철거 도구가 수로·수문을 고른다")]
        [SerializeField] private float edgePickRatio = 0.3f;

        private GameSession _session;
        private GameObject _tileHighlight;
        private GameObject _edgeHighlight;
        private EdgeId? _lastDragEdge;

        public BuildTool Tool { get; set; } = BuildTool.Select;
        public HexCoord? HoverTile { get; private set; }
        public EdgeId? HoverEdge { get; private set; }
        /// <summary>지금 가리키는 시설(변 가까이면 수로·수문, 아니면 타일 시설). 없거나 HUD 위면 null.</summary>
        public Facility HoveredFacility { get; private set; }
        /// <summary>현재 도구를 여기 적용하면 실패하는 이유, 또는 마지막 명령 결과.</summary>
        public string HoverMessage { get; private set; }
        public string LastResult { get; private set; }
        public int SelectedFacilityId { get; set; } = -1;

        private void Awake()
        {
            _session = GetComponent<GameSession>();
        }

        private void Start()
        {
            _tileHighlight = WorldVisuals.HexTile("Hover Tile", transform, validColor);
            _edgeHighlight = WorldVisuals.Primitive(PrimitiveType.Cube, "Hover Edge", transform, validColor);
            _tileHighlight.SetActive(false);
            _edgeHighlight.SetActive(false);
        }

        public static string KindOf(BuildTool tool)
        {
            switch (tool)
            {
                case BuildTool.Well: return BuildingKinds.Well;
                case BuildTool.Farm: return BuildingKinds.Farm;
                case BuildTool.Quarry: return BuildingKinds.Quarry;
                case BuildTool.Mine: return BuildingKinds.Mine;
                case BuildTool.AuctionHall: return BuildingKinds.AuctionHall;
                case BuildTool.Warehouse: return BuildingKinds.Warehouse;
                case BuildTool.Canal: return BuildingKinds.CanalEdge;
                case BuildTool.Gate: return BuildingKinds.CanalGate;
                default: return null;
            }
        }

        public static string Label(BuildTool tool)
        {
            switch (tool)
            {
                case BuildTool.Select: return "선택";
                case BuildTool.Well: return "우물";
                case BuildTool.Farm: return "농장";
                case BuildTool.Quarry: return "채석장";
                case BuildTool.Mine: return "광산";
                case BuildTool.AuctionHall: return "경매장";
                case BuildTool.Warehouse: return "창고";
                case BuildTool.Canal: return "수로";
                case BuildTool.Gate: return "수문";
                case BuildTool.Upgrade: return "개량";
                case BuildTool.Demolish: return "철거";
                case BuildTool.ToggleClose: return "폐쇄/개폐";
                default: return tool.ToString();
            }
        }

        private static bool IsEdgeTool(BuildTool t) => t == BuildTool.Canal || t == BuildTool.Gate;

        private void Update()
        {
            if (!_session.IsReady || _tileHighlight == null) return;
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse == null || cam == null) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Tool = BuildTool.Select;
            if (mouse.rightButton.wasPressedThisFrame) Tool = BuildTool.Select;

            Vector2 pointer = mouse.position.ReadValue();
            bool overHud = HudInputBlocker.IsOverHud(pointer);
            UpdateHover(cam, pointer, overHud);
            if (overHud) return;

            bool pressed = mouse.leftButton.wasPressedThisFrame;
            bool dragging = mouse.leftButton.isPressed && Tool == BuildTool.Canal && HoverEdge.HasValue && HoverEdge != _lastDragEdge;
            if (!mouse.leftButton.isPressed) _lastDragEdge = null;
            if (pressed || dragging)
            {
                Apply();
                if (Tool == BuildTool.Canal) _lastDragEdge = HoverEdge;
            }
        }

        private void UpdateHover(Camera cam, Vector2 pointer, bool overHud)
        {
            HoverTile = null;
            HoverEdge = null;
            HoveredFacility = null;
            var plane = new Plane(transform.up, transform.position);
            Ray ray = cam.ScreenPointToRay(pointer);
            if (overHud || !plane.Raycast(ray, out float distance))
            {
                _tileHighlight.SetActive(false);
                _edgeHighlight.SetActive(false);
                return;
            }

            HexPoint p = _session.ToHexPoint(ray.GetPoint(distance));
            HexCoord tile = _session.Layout.PointToHex(p);
            EdgeId edge = _session.Layout.NearestEdge(p);
            if (_session.Map.Contains(tile)) HoverTile = tile;
            if (_session.Topology.Contains(edge)) HoverEdge = edge;

            // 개량·철거·폐쇄·선택은 변에 충분히 가까우면 수로·수문을 고른다.
            bool pickEdge = IsEdgeTool(Tool);
            if (!pickEdge && HoverEdge.HasValue && EdgeFacility(HoverEdge.Value) != null)
            {
                HexPoint mid = HexTopology.MidpointOf(HoverEdge.Value, _session.Layout);
                double d = Math.Sqrt((mid.X - p.X) * (mid.X - p.X) + (mid.Z - p.Z) * (mid.Z - p.Z));
                pickEdge = d < _session.TileSize * edgePickRatio;
            }

            HoveredFacility = Target(pickEdge);
            CommandResult check = Check(pickEdge);
            HoverMessage = check.Success ? null : check.Message;
            Color color = Tool == BuildTool.Select ? selectColor : check.Success ? validColor : invalidColor;

            _tileHighlight.SetActive(!pickEdge && HoverTile.HasValue);
            _edgeHighlight.SetActive(pickEdge && HoverEdge.HasValue);
            if (_tileHighlight.activeSelf)
            {
                _tileHighlight.transform.position = _session.ToWorld(_session.Layout.TileCenter(tile), 0.02f);
                _tileHighlight.transform.localScale = new Vector3(_session.TileSize * 0.9f, 1f, _session.TileSize * 0.9f);
                WorldVisuals.SetColor(_tileHighlight, color);
            }
            if (_edgeHighlight.activeSelf)
            {
                Vector3 a = _session.ToWorld(HexTopology.PositionOf(edge.VertexA, _session.Layout));
                Vector3 b = _session.ToWorld(HexTopology.PositionOf(edge.VertexB, _session.Layout));
                _edgeHighlight.transform.position = (a + b) * 0.5f + Vector3.up * 0.06f;
                _edgeHighlight.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
                _edgeHighlight.transform.localScale = new Vector3(_session.TileSize * 0.14f, 0.06f, Vector3.Distance(a, b));
                WorldVisuals.SetColor(_edgeHighlight, color);
            }
            _pickEdge = pickEdge;
        }

        private bool _pickEdge;

        /// <summary>가리키는 대상의 시설(변 모드면 수문 우선, 그다음 수로).</summary>
        private Facility Target(bool pickEdge)
        {
            Settlement s = _session.Settlement;
            if (pickEdge) return HoverEdge.HasValue ? EdgeFacility(HoverEdge.Value) : null;
            return HoverTile.HasValue ? s.FacilityAt(HoverTile.Value) : null;
        }

        private Facility EdgeFacility(EdgeId edge) =>
            _session.Settlement.FacilityOn(edge, BuildingKinds.CanalGate) ?? _session.Settlement.FacilityOn(edge, BuildingKinds.CanalEdge);

        private CommandResult Check(bool pickEdge)
        {
            Settlement s = _session.Settlement;
            switch (Tool)
            {
                case BuildTool.Select:
                    return CommandResult.Ok();
                case BuildTool.Canal:
                    return HoverEdge.HasValue ? s.CanBuildCanal(HoverEdge.Value) : CommandResult.Fail("맵 밖입니다.");
                case BuildTool.Gate:
                    return HoverEdge.HasValue ? s.CanBuildGate(HoverEdge.Value) : CommandResult.Fail("맵 밖입니다.");
                case BuildTool.Upgrade:
                case BuildTool.Demolish:
                case BuildTool.ToggleClose:
                    Facility f = Target(pickEdge);
                    if (f == null) return CommandResult.Fail("시설이 없습니다.");
                    if (Tool == BuildTool.Upgrade) return s.CanUpgrade(f.Id);
                    if (Tool == BuildTool.Demolish) return s.CanDemolish(f.Id);
                    if (f.Kind == BuildingKinds.CanalGate) return CommandResult.Ok(); // 수문은 개폐
                    if (s.Rules.CannotClose.Contains(f.Kind)) return CommandResult.Fail($"{f.Kind}은(는) 폐쇄할 수 없습니다.");
                    return f.State == FacilityState.Operating || f.State == FacilityState.Closed
                        ? CommandResult.Ok()
                        : CommandResult.Fail("운영 중이거나 폐쇄된 시설만 바꿀 수 있습니다.");
                default:
                    return HoverTile.HasValue ? s.CanBuild(KindOf(Tool), HoverTile.Value) : CommandResult.Fail("맵 밖입니다.");
            }
        }

        private void Apply()
        {
            Settlement s = _session.Settlement;
            CommandResult r;
            Facility f;
            switch (Tool)
            {
                case BuildTool.Select:
                    f = Target(_pickEdge);
                    SelectedFacilityId = f?.Id ?? -1;
                    return;
                case BuildTool.Canal:
                    if (!HoverEdge.HasValue) return;
                    r = s.RequestCanal(HoverEdge.Value);
                    break;
                case BuildTool.Gate:
                    if (!HoverEdge.HasValue) return;
                    r = s.RequestGate(HoverEdge.Value);
                    break;
                case BuildTool.Upgrade:
                    f = Target(_pickEdge);
                    r = f != null ? s.RequestUpgrade(f.Id) : CommandResult.Fail("시설이 없습니다.");
                    break;
                case BuildTool.Demolish:
                    f = Target(_pickEdge);
                    r = f != null ? s.RequestDemolish(f.Id) : CommandResult.Fail("시설이 없습니다.");
                    break;
                case BuildTool.ToggleClose:
                    f = Target(_pickEdge);
                    if (f == null) { r = CommandResult.Fail("시설이 없습니다."); break; }
                    r = f.Kind == BuildingKinds.CanalGate
                        ? s.SetGateOpen(f.Id, !f.GateOpen)
                        : s.SetClosed(f.Id, f.State == FacilityState.Operating);
                    break;
                default:
                    if (!HoverTile.HasValue) return;
                    r = s.RequestBuild(KindOf(Tool), HoverTile.Value);
                    break;
            }
            LastResult = r.Success ? $"{Label(Tool)}: 예약/적용됨" : $"{Label(Tool)}: {r.Message}";
        }
    }
}
