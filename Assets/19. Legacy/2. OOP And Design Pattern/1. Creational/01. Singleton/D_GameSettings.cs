using System;
using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Creational
{
    // Singleton - ScriptableObject 싱글톤 (데이터/설정 전용 형태)
    // 상황 : 볼륨,그래픽 세팅 같은 "전역 설정 데이터"를 에셋 하나로 관리/공유
    //       (전역적으로 영향을 미치는 게임데이터도 OK)
    // 형태 : ScriptableObject를 특정 경로(Resources/Addressable)에서
    //       로드 후 static으로 캐싱.
    
    // 최초 사용 법
    // : 스크립터블 오브젝트를 에디터에서 생성하고 지정된 경로에 위치시키면 됨=
    
    [CreateAssetMenu(fileName = "MyGameSetting", 
        menuName = "OOP And Design Pattern/Game Settings")]
    public class GameSettings : ScriptableObject
    {
        private static GameSettings instance;
        public static GameSettings Instance 
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<GameSettings>("GameSettings");
                }
                return instance;
            }
        }
        
        [Header("Global Settings")]
        public float BGMVolume;
        public float SFXVolume;
        public int GraphicQuality;
    }

    public class GameSettingDemo : MonoBehaviour
    {
        private void Start()
        {
            GameSettings settings = GameSettings.Instance;
            Debug.Log(settings.BGMVolume);
            Debug.Log(settings.SFXVolume);
            Debug.Log(settings.GraphicQuality);
        }
    }
}