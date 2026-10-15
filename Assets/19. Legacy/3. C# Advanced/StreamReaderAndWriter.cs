using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Study.CsharpAdv.FileIO
{
    public class StreamReaderAndWriter : MonoBehaviour
    {
        public string FilePath;

        private void Start()
        {
            UnityFilePath();
        }

        private void Write()
        {
            StreamWriter writer = new StreamWriter(FilePath);
            writer.WriteLine("첫 번째 줄");
            writer.WriteLine("두 번째 줄");
            writer.WriteLine("세 번째 줄");
            writer.WriteLine("네 번째 줄");
            writer.Close();
        }

        private void WriteWithUsing()
        {
            // StreamWriter, Reader는 다 쓰면 Dispose()라는거를 호출해줘야하는
            // 자원입니다. 그래서 Using문 (Using Block 문)을 이용해서 
            // 파일을 읽거나 쓰는 도중 에러가 발생하더라도 안전하게 닫을 수 있는
            // 방법으로 사용합니다.
            using (StreamWriter writer = new StreamWriter(FilePath))
            {
                writer.WriteLine("첫 번째 줄");
                writer.WriteLine("두 번째 줄");
                writer.WriteLine("세 번째 줄");
                writer.WriteLine("네 번째 줄");
                writer.Close();
            }
        }
        
        private void Read()
        {
            StreamReader reader = new StreamReader(FilePath);
            List<string> lines = new List<string>();
            while (reader.EndOfStream == false)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                lines.Add(line);
            }
            for (int i = 0; i < lines.Count; i++)
            {
                Debug.Log("Line: " + lines[i]);
            }
        }

        private void UnityFilePath()
        {
            // 유니티에서 기본적으로 제공하는 주요 경로 프로퍼티들은 아래와 같습니다.
            // 에디터, 빌드, 플랫폼에 따라 상대적인 경로로 잡혀있습니다.
            // 1. Application.dataPath : 에디터만, Assets 폴더 위치, 빌드하면 위치가 완전히 달라짐
            Debug.Log(Application.dataPath);
            
            // 2. Application.persistentDataPath : 읽고 쓸수있음. 앱 삭제전까지. OS에 의해 앱이 자유롭게
            //  사용할 수 있는 특정 구역이라고 생각하면됨. Window, Android 등의 플랫폼별로 경로는 달라지지만
            //  없어서 못쓰는 경우는 없음. 보통 사용자 디바이스에 저장기록을 남길때 사용함.
            Debug.Log(Application.persistentDataPath);
            //FilePath = Application.persistentDataPath + "/test.txt";
            //WriteWithUsing();
            
            // 3. Application.streamingAssetsPath : 읽기전용 경로. 플랫폼에 따라서 경로는 상대적임.
            //   안드로이드의 경우에는 apk파일 내부에 존재함. 에디터에서는 Assets/StreamingAssets/ 폴더를
            //   생성하여 사용
            Debug.Log(Application.streamingAssetsPath); 
            
            
            // 4. 특별한 경로 Resources.Load : 빌드에서도 사용가능. 수정 불가능. 해당 공간은 특별해서
            //    텍스트 경로를 통해 접근하는게 아니라 앱의 특정 구역에 접근하도록 강제되어있습니다.
            //    Editor에 Assets/Resources 라는 폴더를 만들고 사용합니다. 빌드 용량이 늘어납니다.
            
            // 아래코드는 Resources/Triangle 파일을 메모리에 불러옵니다
            GameObject triangle = Resources.Load<GameObject>("Triangle");
            
            // 메모리에 저장된 원본을 가지고 씬에 생성을 합니다
            Instantiate(triangle, new Vector3(5, 4, 0), Quaternion.identity);
        }
        
    }
}