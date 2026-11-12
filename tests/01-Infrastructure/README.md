# Infrastructure 테스트

Infrastructure의 공개 API 계약은 NUnit으로, 상태가 이어지는 흐름은 UniTest로 검증한다. 모듈별 표·계획·결과는 각 테스트 폴더의 docs에 둔다.

## 모듈 문서

| 모듈 | 계획 | 결과 |
| --- | --- | --- |
| Configuration | [계획](01-M.1-Configuration/docs/02.%20Plans.md) | [결과](01-M.1-Configuration/docs/03.%20Results.md) |
| Objects | [계획](02-O.1-Objects/docs/02.%20Plans.md) | [결과](02-O.1-Objects/docs/03.%20Results.md) |
| 표시 유틸리티 | [계획](03-M.1-Utilities/docs/02.%20Plans.md) | [결과](03-M.1-Utilities/docs/03.%20Results.md) |
| JSON 변환·등록 | [계획](03-M.1-Utilities/Json/docs/02.%20Plans.md) | [결과](03-M.1-Utilities/Json/docs/03.%20Results.md) |
| ExceptionHandler | [계획](03-M.2-ExceptionHandler/docs/02.%20Plans.md) | [결과](03-M.2-ExceptionHandler/docs/03.%20Results.md) |
| Log | [계획](03-M.3-LogSystem/docs/02.%20Plans.md) | [결과](03-M.3-LogSystem/docs/03.%20Results.md) |

## 공통 검증

여러 모듈을 함께 다루는 실행 절차·명령은 [통합 테스트 계획](docs/02.%20Plans.md)에, 실행별 통과·실패와 원본은 [통합 검증 결과](docs/03.%20Results.md)에 둔다. 프로젝트 구성과 테스트 작성 기준은 [공통 테스트 안내](../README.md)를 따른다.
