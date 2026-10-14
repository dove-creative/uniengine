# AGENTS

## 기본 규칙

- 한국어 대화에서 대화에 존댓말을 사용한다.
- 프로젝트 문서는 `docs` 폴더에 존재한다. 작업 시작 전, 관련 문서가 존재하는지 확인하고, 존재할 경우 해당 내용을 숙지한 후 작업을 시작한다.
- 공통 컨벤션과 작업 지침은 `guidelines/blackthunder-conventions`에 존재한다. 해당 경로가 없거나 비어 있다면 서브모듈이 초기화되지 않았을 수 있으므로 사용자에게 알린다.
- Twin Turbo 자료구조 또는 Composition 관련 작업을 시작하기 전, `references/twin-turbo-data-structures`의 관련 문서와 샘플을 확인한다. 해당 경로가 없거나 비어 있다면 서브모듈이 초기화되지 않았을 수 있으므로 사용자에게 알린다.
- TTDS 자체의 개발은 Golden Hind와 같은 상위 폴더의 `../twin-turbo-data-structures`에서 진행한다. `references/twin-turbo-data-structures`는 참조용이며 직접 개발하지 않는다. 독립 저장소에서 검증·커밋·푸시한 원격 커밋으로 수동 갱신하고, 변경된 서브모듈 포인터를 Golden Hind에 커밋한다. 절차는 참조 저장소의 `docs/development.md`를 따른다.

## 컨벤션

- 이 작업공간에서 코드 파일을 새로 작성하거나 수정할 때는 줄 끝을 Unix 형식인 `LF`로 유지한다.
- 일부 코드 파일은 UTF-8이 아닐 수 있다. `apply_patch`가 `invalid utf-8 sequence`로 실패하면 파일 손상으로 판단하지 말고, PowerShell/.NET에서 기존 인코딩으로 읽은 뒤 필요한 범위만 UTF-8로 저장한다(.md 등 단순 문서 파일의 경우 인코딩 검사를 하지 않아도 된다).
- C# 코드 작업 시, 별도 지침이 없다면 `guidelines/blackthunder-conventions/conventions/dove CSharp convention.md`를 따른다.
- 문서 작업 시, 별도 지침이 없다면 `guidelines/blackthunder-conventions/conventions/dove documentation convention.md`를 따른다.
- 작업 유형에 해당하는 구체적인 지침이 `guidelines/blackthunder-conventions/instructions`에 존재한다면 해당 지침을 따른다.
- 코드 내 주석은 영어를 사용한다.
- Git Commit 시 제목은 영어를 사용한다.

## 지침

`twin-turbo-data-structures` 관련 작업 시, 썸네일 이미지를 새로 생성하지 않는다.

## 정보

- Unity 등 최신 C# 문법을 완전히 지원하지 않는 프레임워크와의 호환성을 위해 nullable 표기법을 사용하지 않는다. 이로 인해 발생하는 nullable 관련 경고는 무시할 수 있다.
- 사용자가 명시적으로 테스트 실행을 요청하지 않은 경우 테스트를 실행하지 않는다. 필요한 확인은 빌드, 정적 검색, 문서 검사처럼 테스트가 아닌 검증으로 한정하고, 테스트가 필요하면 먼저 사용자에게 확인한다.
