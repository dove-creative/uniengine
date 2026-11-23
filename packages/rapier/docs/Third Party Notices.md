# Rapier Native Component — Third-Party Notices

The UniEngine native integration uses Rapier and Parry. These upstream components remain licensed under the Apache License, Version 2.0. UniEngine's MIT license does not replace their licenses.

## Components

| Component | Published version | Cargo packages | License |
| --- | --- | --- | --- |
| [Rapier](https://github.com/dimforge/rapier) | 0.36.1 | rapier2d, rapier2d-f64, rapier3d, rapier3d-f64 | [Apache-2.0 text](../licenses/rapier/LICENSE) |
| [Parry](https://github.com/dimforge/parry) | 0.31.1 | parry2d, parry2d-f64, parry3d, parry3d-f64 | [Apache-2.0 text](../licenses/parry/LICENSE) |

All eight published Cargo manifests declare `Apache-2.0`. The full license texts above were copied without changing their contents from the upstream repositories:

- [Rapier LICENSE](https://github.com/dimforge/rapier/blob/475caa3ff4646fd9a796b67d14116256f4962bc7/LICENSE), at the commit recorded in the published crate's VCS metadata.
- [Parry LICENSE](https://github.com/dimforge/parry/blob/3383f51cbbe9af70565427e7a66c605e1557c1fc/LICENSE), at the commit recorded in the published crate's VCS metadata.

The published crates record `dirty: true`; those VCS commits identify the license-text sources and do not establish byte-for-byte identity between a Git checkout and the published source packages. Package versions and archive checksums are recorded separately in Cargo.lock and the [package provenance](../../README.md).

On 2026-11-23, no separate `NOTICE` file was found in the eight downloaded Rapier/Parry crate archives or at either of those upstream repository roots. This observation does not cover other dependencies or future versions. This document is a UniEngine component notice, not an upstream-provided NOTICE file.

## Redistribution

When distributing a native DLL containing Rapier or Parry, include the applicable Apache-2.0 license text with the distribution. Keep these component and version notices with it so recipients can identify the covered software. A web link alone does not replace the required license copy.

Preserve applicable upstream copyright, patent, trademark, and attribution notices in redistributed source. If an upstream distribution supplies a NOTICE file, carry forward its applicable attribution notices as required by Apache-2.0 section 4.

Current native builds are investigation artifacts; no production DLL has been adopted under `dist` yet. Before adopting a distributable DLL, also inspect the licenses and required notices of the other dependencies and any runtime code actually included in that binary. This document covers Rapier and Parry and is not a complete license inventory for the entire native dependency graph or UniEngine.

## Modified upstream files

The stage 0 investigation applies UniEngine revision `0.36.1-r1` to the four Rapier 0.36.1 packages. Parry 0.31.1 remains unmodified. The preserved upstream archives are under `upstream/0.36.1`; generated modified sources are separate from the Cargo registry cache.

In each Rapier package, UniEngine modified `src/pipeline/physics_world.rs` and `src/pipeline/physics_pipeline/substep.rs` on 2026-11-23 to refresh physics bookkeeping and initialize persistent islands during collision-only updates. Each changed location carries an English modification notice identifying UniEngine, the date, and revision. The [patch](../revisions/0.36.1-r1/collision-update.patch), [provenance](../revisions/0.36.1-r1/provenance.json), [plan](revisions/0.36.1-r1/Plan.md), and [results](investigation/03.%20Results.md) identify the changes and verified scope. The upstream package versions remain 0.36.1; this is not an official upstream release.

If an upstream file is changed for a local patch, place a prominent notice in the changed file identifying that it was modified. Keep the patch revision, affected files, and provenance with the revision plan, and record actual verification results in the [investigation results](investigation/03.%20Results.md). Revisions contains only the problem, solution summary, status, and links. The central records supplement the changed-file notice; they do not replace it.

The governing requirements are in [Apache License 2.0, section 4](https://www.apache.org/licenses/LICENSE-2.0).

## License scope

UniEngine's original integration code remains subject to the [UniEngine MIT license](../../../LICENSE.md). Rapier, Parry, and other third-party components retain their respective licenses. These notices do not relicense upstream code.
