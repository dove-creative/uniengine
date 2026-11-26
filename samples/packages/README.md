# 샘플 NuGet 패키지

샘플 실행에 필요한 외부 NuGet 패키지와 하위 의존성을 보관하는 로컬 피드이다. 원본 .nupkg를 수정하지 않고 Git으로 관리하며, 현재 채택한 버전만 보관한다.

## 구성

| 범위 | 보관 내용 |
| --- | --- |
| GeoSystem 시각화 | Avalonia 12.1.6, SkiaSharp 3.119.4, HarfBuzzSharp 8.3.1.3과 하위 의존성 |
| 참조하는 UniEngine 본체 | System.Text.Json 10.0.14와 하위 의존성 |
| 자사 패키지 | 저장소 루트의 [packages](../../packages/README.md)에서 관리 |

외부 패키지 33개, 총 298,283,792바이트(약 284MiB)를 보관한다. Avalonia.Desktop의 Windows·Linux·macOS 및 SkiaSharp·HarfBuzzSharp의 플랫폼별 의존성을 포함한다. 패키지별 ID·버전·원본 피드·SHA-256·라이선스 표기는 [source-manifest.json](source-manifest.json)에 기록한다. license가 파일명인 항목은 해당 패키지 내부 파일을 따른다. 원본 패키지에 포함된 라이선스와 고지는 그대로 유지한다.

## 복원과 실행

.NET 10 SDK가 설치되어 있어야 한다. 명령은 저장소 최상위에서 실행한다.

```powershell
dotnet run --project samples/03-Services/GeoSystem/UniEngine.GeoSystem.Sample.csproj -c Release -p:RestoreConfigFile=samples/NuGet.config
dotnet run --project samples/UniEngine.Samples.csproj -p:RestoreConfigFile=samples/NuGet.config -- all
```

[샘플 NuGet 설정](../NuGet.config)은 이 폴더와 저장소 루트의 packages만 패키지 소스로 사용한다. RestoreConfigFile을 지정하면 샘플이 참조하는 본체에도 같은 설정을 적용한다. 필요한 패키지가 빠졌으면 복원에 실패하며 인터넷 피드로 대체하지 않는다. 명시한 설정을 생략하면 참조하는 본체에는 저장소 루트의 일반 NuGet 설정이 적용될 수 있다.

복원은 보관된 패키지를 일반 NuGet 캐시에 풀어 사용하는 과정이다. 이 폴더에는 .nupkg와 출처 문서만 두며, 풀린 캐시와 bin·obj는 버전 관리하지 않는다. 두 로컬 피드에는 취약점 데이터가 없으므로 이 오프라인 복원은 온라인 취약점 검사를 수행하지 않는다.

## 갱신

1. 필요한 패키지 버전을 지정하고, NuGet.org를 사용할 수 있는 별도 검증 환경에서 직접·하위 의존성을 모두 복원한다. 이때 온라인 취약점 검사도 수행한다.
2. 샘플 프로젝트와 참조하는 본체의 project.assets.json에서 실제 의존성 전체를 확인한다.
3. 외부 패키지의 원본 .nupkg를 이 폴더에 추가하고 source-manifest.json의 출처·버전·SHA-256·라이선스를 갱신한다. 같은 버전의 파일을 다른 내용으로 덮어쓰지 않는다.
4. 샘플 설정을 지정하여 빈 캐시와 HTTP 캐시를 사용하지 않는 조건에서 복원·빌드하고 관련 검증을 수행한다.
5. 사용하지 않는 이전 버전은 제거하고, 새 패키지·참조·출처 문서를 함께 반영한다.

캐시를 비운 검증에서는 --packages로 검증 전용 경로를 지정할 수 있다. 검증 후에는 일반 복원을 다시 수행하여 obj가 임시 검증 경로를 계속 참조하지 않도록 한다.
