using System.Collections.Generic;
using UnityEngine;

namespace Study_OOP_And_Design_Pattern.Structural.Composite
{
    // Composite - 예시 A : 파일/폴더 트리
    // 상황 : 파일(개별, Leaf)과 폴더(묶음, Composite)를
    //       같은 방식으로 다룬다(크기, 계산, 출력)
    // 형태 : 잎(File)과 가지(Folder)가 같은 인터페이스.
    //       가지는 자식에게 재귀 형태로 위임을 한다

    public interface IFileNode
    {
        int Size();
        void Print(int depth);
    }
    
    public class File : IFileNode
    {
        private string name;
        private int size;

        public File(string name, int size)
        {
            this.name = name;
            this.size = size;
        }
        
        public int Size() => this.size;

        public void Print(int depth)
        {
            string empty = new string(' ', depth * 2);
            Debug.Log($"{empty} - {name} : size = {size}");
        }
    }

    public class Folder : IFileNode
    {
        private string name;
        private List<IFileNode> children = new List<IFileNode>();
        
        public Folder(string name) { this.name = name; }

        public Folder Add(IFileNode fileNode)
        {
            children.Add(fileNode);
            return this;
        }

        public int Size()
        {
            int sum = 0;
            for (int i = 0; i < children.Count; i++)
                sum += children[i].Size();
            return sum;
        }

        public void Print(int depth)
        {
            string empty = new string(' ', depth * 2);
            Debug.Log($"{empty} - {name} : size = {Size()}");

            for (int i = 0; i < children.Count; i++)
                children[i].Print(depth + 1);
        }
    }

    public class FileTreeDemo : MonoBehaviour
    {
        private void Start()
        {
            Folder root = new Folder("A")
                .Add(new File("교안A.txt", 4))
                .Add(new Folder("B")
                    .Add(new File("교안B-1.txt", 4))
                    .Add(new File("교안B-2.txt", 4)))
                .Add(new Folder("C")
                    .Add(new File("교안C-1.txt", 4)))
                .Add(new Folder("D"))
                .Add(new File("기타 교안 F-1", 16))
                .Add(new File("기타 교안 F-2", 16))
                .Add(new File("기타 교안 F-3", 16));

            
            root.Print(0);
        }
    }
}