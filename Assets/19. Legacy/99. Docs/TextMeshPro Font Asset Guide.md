# TextMeshPro Font Asset - Custom Range (Decimal) Preset

Unity TextMeshPro(Font Asset Creator)의 
**Character Set → Custom Range** 설정에 사용할 수 있는 
**10진수 기반 Unicode 범위 Preset**

## 사용 방법

1. Unity에서 `Window > TextMeshPro > Font Asset Creator` 열기
2. `Character Set`을 `Custom Range`로 설정
3. 아래 내용을 `Unicode Range (Decimal)` 입력란에 붙여넣기
4. 나머지 설정을 조정하고 `Generate Font Atlas` 후 `Save`

---

## Custom Range 목록 (Decimal)

``` txt
- 한글 완성형 (가~힣)
44032-55203

- 한글 자모 (호환용 자모 ㄱ~ㆎ)
12593-12643

- 한글 자모 (조합형 초성, 중성, 종성)
4352-4607

- 숫자 0~9
48-57

- 알파벳 (대문자 A~Z, 소문자 a~z)
65-90
97-122

- 기본 특수기호 및 문장부호
32-47     // 공백, !"#$%&'()*+,-./
58-64     // :;<=>?@
91-96     // [\]^_`
123-126   // {|}~

- 유용한 문장 부호/기호
8220-8221   // “ ”
8216-8217   // ‘ ’
8230        // … (ellipsis)
183         // · (가운뎃점)

- TMP 내부 스타일 문자
818         // U+0332: Combining Low Line (밑줄 효과)
9647        // U+25A3: 대체 문자 (□)
```

```txt
44032-55203,
12593-12643,
4352-4607,
48-57,
65-90,
97-122,
32-47,
58-64,  
91-96,  
123-126,
8220-8221,
8216-8217,
8230,     
183,
818,  
9647,       
```