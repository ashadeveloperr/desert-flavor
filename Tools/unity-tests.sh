#!/usr/bin/env bash
# Unity 테스트를 '테스트 전용 프로젝트 사본'에서 배치 모드로 실행한다.
#  - 사용자가 에디터로 이 프로젝트를 열고 작업 중이어도 방해하지 않는다(Unity는 한 프로젝트를 한 프로세스만 열 수 있어서,
#    같은 프로젝트에 배치 모드를 돌리면 잠금 때문에 실패하고, 열린 에디터에 테스트를 시키면 작업이 끊긴다).
#  - 사본 위치: 저장소 루트의 .unity-testcopy/ (git 무시). 실행할 때마다 Assets·Packages·ProjectSettings를
#    작업 트리 그대로(커밋 안 한 변경 포함) 미러 복사하고, 사본은 자기 Library를 따로 쓴다.
#    처음 한 번은 사본의 Library 임포트에 몇 분 걸린다. 이후에는 바뀐 파일만 복사·재임포트한다.
#  - 에디터에서 저장하지 않은 변경(씬·에셋)은 디스크에 없으므로 사본에도 들어가지 않는다.
# 사용: bash Tools/unity-tests.sh [EditMode|PlayMode|All]
# 종료 코드: 0 = 모두 통과, 1 = 실패 있음(컴파일 오류 포함), 4 = 사본 동기화 실패
set -u
ROOT=$(cd "$(dirname "$0")/.." && pwd)
COPY="$ROOT/.unity-testcopy"
MODE=${1:-All}
# Unity 6000.3.15f1 경로. 다른 곳에 설치했으면 UNITY_EXE 환경 변수로 덮어쓴다.
UNITY="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.3.15f1/Editor/Unity.exe}"
if [ ! -f "$UNITY" ]; then echo "[unity-tests] Unity를 찾을 수 없습니다: $UNITY (UNITY_EXE로 지정)"; exit 4; fi

mkdir -p "$COPY/Logs"
for d in Assets Packages ProjectSettings; do
  # /MIR: 사본을 원본과 똑같이(지운 파일도 지움). 시간·크기가 같은 파일은 건너뛰어 빠르다.
  MSYS_NO_PATHCONV=1 robocopy "$(cygpath -w "$ROOT/$d")" "$(cygpath -w "$COPY/$d")" /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:1 > /dev/null
  rc=$?
  if [ $rc -ge 8 ]; then echo "[unity-tests] 사본 동기화 실패: $d (robocopy $rc)"; exit 4; fi
done
echo "[unity-tests] 사본 동기화 완료: $COPY"

# 원본에 새로 추가한 파일의 .meta는 사본의 Unity가 만든다. 실행 뒤 원본에 없는 .meta(원본에 짝 파일이 있는 것만)를 되가져와
# 다음 동기화(/MIR)가 지우고 GUID를 새로 만드는 일을 막는다.
copy_back_meta() {
  (cd "$COPY" && find Assets -name "*.meta") | while read -r meta; do
    src="${meta%.meta}"
    if [ ! -e "$ROOT/$meta" ] && [ -e "$ROOT/$src" ]; then
      cp "$COPY/$meta" "$ROOT/$meta" && echo "[unity-tests] 새 .meta를 원본에 복사: $meta"
    fi
  done
}

status=0
modes=$MODE; [ "$MODE" = "All" ] && modes="EditMode PlayMode"
for m in $modes; do
  lower=$(echo "$m" | tr '[:upper:]' '[:lower:]')
  results="$COPY/Logs/$lower-results.xml"
  log="$COPY/Logs/$lower.log"
  rm -f "$results"
  extra=(); [ "$m" = "EditMode" ] && extra=(-nographics)
  "$UNITY" -batchmode "${extra[@]}" -projectPath "$(cygpath -w "$COPY")" -runTests -testPlatform "$m" \
    -testResults "$(cygpath -w "$results")" -logFile "$(cygpath -w "$log")"
  code=$?
  if [ -f "$results" ]; then
    # 요약과 실패한 테스트(이름·메시지)를 출력한다.
    powershell -NoProfile -Command "\$x = [xml](Get-Content -Encoding UTF8 '$(cygpath -w "$results")'); \$r = \$x.'test-run';
      '[unity-tests] $m exit $code total=' + \$r.total + ' passed=' + \$r.passed + ' failed=' + \$r.failed + ' skipped=' + \$r.skipped;
      \$x.SelectNodes('//test-case[@result=''Failed'']') | ForEach-Object { '  - ' + \$_.fullname; '    ' + (\$_.failure.message.'#cdata-section' -replace '\s+', ' ').Trim() }"
  else
    echo "[unity-tests] $m exit $code — 결과 파일 없음. 컴파일 오류라면:"
    grep -m 10 "error CS" "$log" 2>/dev/null | sed 's/^/  /'
    [ -f "$log" ] || echo "  로그도 없음(Unity 실행 실패)"
  fi
  [ $code -ne 0 ] && status=1
done
copy_back_meta
exit $status
