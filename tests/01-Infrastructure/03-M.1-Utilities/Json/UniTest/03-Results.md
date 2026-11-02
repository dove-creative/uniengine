# JSON 변환 검증 결과

## 목차

1. 검증 결과
2. 실행 환경과 명령
3. 확인된 범위

## 검증 결과

2026-11-02 통합 실행의 TRX에서 JsonConverterTests와 JsonSerializerOptionsExtensionsTests 두 클래스의 결과를 추출한 기록이다.

| 검증 | 결과 |
| --- | --- |
| JSON 변환·등록 NUnit 사례 | 269개 통과, 실패 0개, 건너뜀 0개 |
| 제품 결함 | 해당 검증 범위에서 발견하지 않음 |
| 소유 코드·문서 파일 형식 | LF 확인 |

제품 코드는 변경하지 않았다. 내부 공통 도구는 8개 공개 변환기를 통해 간접 검증했다.

## 실행 환경과 명령

Windows의 .NET SDK 10.0.403에서 Release 테스트를 실행했다. 기존 NUnit 3 실행기와 단일 GoldenHind.Tests 프로젝트를 사용했다.

```powershell
& 'C:/Users/user/AppData/Local/Microsoft/dotnet/dotnet.exe' test tests/GoldenHind.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=Infrastructure-Full.trx' --results-directory tests/bin/TestResults
```

검증 산출물은 tests/bin/TestResults/Infrastructure-Full.trx이며 버전 관리에는 포함하지 않는다. 이 문서의 사례 수는 해당 TRX에서 두 JSON 테스트 클래스에 속한 결과만 센 값이다.

## 확인된 범위

8개 형식의 고정 JSON과 왕복, 중첩 속성을 포함한 순서·대소문자 변경, 최상위 성분별 누락·중복·타입 오류, 미지원 속성·불완전 객체를 검증했다. Int32 양쪽 경계와 범위 초과·소수, 유한 float 양쪽 경계·최소 양수, 비유한 값 읽기·쓰기와 잘못된 중첩 Transform도 확인했다.

Color는 정규화된 성분, byte 왕복, 임의 실수 양자화, 네 성분 각각의 범위 오류를 확인했다. 등록 확장은 null 거부, 8개 변환기의 단일 등록, 동일 옵션 반환, 사용자 변환기의 위치·인스턴스·실제 동작 보존과 Transform 내부 변환 경로를 확인했다.
