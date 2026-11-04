# Configuration 테스트 계획

## 목차

1. 실행 구성
2. 격리와 정리

## 실행 구성

[ConfigurationTests](../ConfigurationTests.cs)의 NUnit 사례 6개와 [ConfigurationStateProjectTests](ConfigurationStateProjectTests.cs)의 UniTest 사례 1개를 단일 GoldenHind.Tests 프로젝트에서 실행한다. UniTest 상태 전이 구현은 이 UniTest 폴더에서 표·계획·결과와 함께 관리한다. 본체 공개 API를 참조하며 초기화 해제나 private 상태 변경은 사용하지 않는다.

UniTest는 CompactProject&lt;Model&gt;·Inline 방식으로 네 단계를 구성한다. 프레임워크가 이전 Lab을 재실행하므로 각 Lab은 해당 단계까지의 전체 시나리오를 새 프로세스에서 재현한다. 성공·취소 여부와 마지막 ExecutionCount가 4인지 검사하며 실패에는 JSON 보고서를 포함한다.

## 격리와 정리

각 프로세스에는 고유 GUID 기반 애플리케이션 ID를 전달한다. 경로 생성 실패는 테스트 전용 프로젝트 경로에 파일을 만들어 유도한다. 부모 실행기는 30초 제한, 종료 코드, 표준 출력과 오류를 확인한다. 정상·실패·시간 초과 뒤에는 부모가 검증한 정확한 테스트 소유 경로만 정리한다.
