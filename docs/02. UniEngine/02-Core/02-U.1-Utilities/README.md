# Level 2 Utilities

Hierarchy의 Core Level 2에 배치된 수학·데이터 유틸리티 설계 항목이다.

## 목차

- 소개
- 정의된 범위

## 소개

- 개요: Core Level 2의 수학·데이터 유틸리티를 다루는 설계 영역이다.
- 목적: 수학·데이터 처리 책임을 공통 도구로 배치하기 위한 분류이다.
- 구현 상태: 현재 src에 대응 구현이 없으며 각 항목에서 정의된 역할과 미정 범위를 구분한다.

## 정의된 범위

| 분류 | 항목 |
| --- | --- |
| Math | [VectorFunctions](Math/VectorFunctions.md), [CompareValue](Math/CompareValue.md) |
| Utilities | [DataComposer](DataComposer.md), [DataFormatter](DataFormatter.md) |

현재 src에 대응 구현이 없으므로 이 폴더는 문서상의 분류이다. Infrastructure의 DataFormattingExtensions는 실제 구현이 있는 별개 객체이며 Core의 DataFormatter와 동일한 구현으로 취급하지 않는다.

[Core 구성](../README.md)
