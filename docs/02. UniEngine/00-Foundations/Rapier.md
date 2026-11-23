# Rapier

UniEngine의 PhysicalSpace가 2D·3D 충돌·물리·기하 계산에 사용하는 외부 네이티브 모듈이다.

## 목차

- 소개
- 연결과 관리
- 참조

## 소개

| 항목 | 설명 |
| --- | --- |
| 개요 | Rapier와 그 기하 라이브러리 Parry의 기능을 C ABI로 노출한 네이티브 라이브러리이다. |
| 목적 | PhysicalSpace가 원본의 충돌·물리·기하 기능을 컴파일된 형식으로 호출할 수 있게 한다. |
| 구현 | Rapier·Parry를 고정 버전 의존성으로 사용하는 UniEngine 전용 Rust 연결 DLL을 만들고, 본체의 PhysicalSpace가 이를 호출한다. 현재 0단계는 Rapier 로컬 수정본 0.36.1-r1과 수정하지 않은 Parry 0.31.1을 검증한다. |

## 연결과 관리

Rapier·Parry의 설계와 구현은 원본 저장소가 소유하며, 두 라이브러리의 라이선스는 Apache-2.0이다. 현재 검증본은 Rapier 로컬 수정본 0.36.1-r1과 Parry 원본이며 공식 C 바인딩 안에 코드를 추가하지 않는다. 현재 `packages/rapier`의 Rust 실험 프로젝트와 빌드 스크립트가 공개 Rust API를 UniEngine 소유의 C ABI로 연결한다. 후속 [DLL 이주 계획](../../../packages/rapier/docs/investigation/DLL%20Migration%20Plan.md)은 외부 자료를 packages에 남기고 전용 구현·빌드를 PhysicalSpace 모듈로 옮기는 방향이며 아직 적용하지 않았다. 제품 연결 DLL은 월드·도형과 핸들 검증·오류 전달·콜백·자원 해제를 담당한다.

충돌 검사 이후 운동 갱신 문제에 Rapier 0.36.1의 로컬 패치를 적용하고 네 구성에서 검증했다. 무수정 dt=0 대안도 초기 수면 전환에 실패하여 PhysicsWorld의 갱신 경로와 persistent island 초기화를 수정했다. 별도 Rust 연결 구조와 Parry 원본은 유지한다. 문제와 해결방안은 [수정 기록](../../../packages/rapier/docs/revisions/Revisions.md), 적용 절차는 [수정본 계획](../../../packages/rapier/docs/revisions/0.36.1-r1/Plan.md), 실제 범위는 [결과](../../../packages/rapier/docs/investigation/03.%20Results.md)에 둔다.

기준 버전은 Rapier 0.36.1과 Parry 0.31.1이다. Cargo 의존성을 정확한 버전으로 지정하고 Cargo.lock으로 전이 의존성과 패키지 체크섬을 고정한다. 배포 crate의 원본 커밋도 확인하여 출처에 기록한다. Rapier의 비교 기준 태그 `v0.36.1`은 커밋 `3406750a38286a0d3c7d0f4be2f9d53153e81d90`이다. 공식 C 바인딩의 버전·ABI·오류 API는 채택 계약에 포함하지 않는다.

실제 배포 crate는 Rapier `475caa3ff4646fd9a796b67d14116256f4962bc7`, Parry `3383f51cbbe9af70565427e7a66c605e1557c1fc`를 출판 시점 커밋으로 기록하며 모두 `dirty: true`이다. 이는 배포자 메타데이터이며 UniEngine이 원본을 수정했다는 뜻이 아니다. 태그의 깨끗한 checkout과 동일하다고 단정하지 않고, Cargo.lock의 .crate 체크섬을 재현 기준으로 삼는다. 0단계에서 원본 압축 파일과 실제 빌드 입력 파일의 일치를 확인했다.

제품 채택 후 UniEngine의 일반 빌드와 테스트는 PhysicalSpace 모듈의 `Native/dist`에 채택한 연결 DLL을 사용하고 Rust 툴체인을 요구하지 않는다. C# 객체 API는 UniEngine.dll에 포함하며 네이티브 DLL은 별도 파일로 함께 배포한다. 현재는 0단계의 독립 연결 실험이고 본체에 채택한 배포본은 없다. 1차 빌드·검증 플랫폼은 Windows x64이며, 이주 후에도 원본·패치·라이선스·조사 기록은 packages/rapier에서 관리한다. 연결 DLL 버전·원본 버전과 커밋·빌드 옵션·SHA-256·검증 기록은 채택 배포본과 함께 보존한다.

## 참조

[Rapier 저장소](https://github.com/dimforge/rapier) · [PhysicalSpace](../01-Infrastructure/03-M.4-PhysicalSpace/README.md) · [패키지 출처와 갱신 절차](../../../packages/README.md) · [Foundations](README.md)
