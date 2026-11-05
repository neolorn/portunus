# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `SecretAddress`, `Secret`, `AgentStatus`, `Result`, `Result<T>`, `Failure`, `SecretsStartupException`, `ISecretClient`, `AgentState`, `WaitReason` and `SetSource`, the public types of `Portunus.Core`.
- The layout's check of a set, every finding in one pass, ordered by item and field (`03` PRT-VLT-007).
- The wire formats of the requests, the answers and the push, each one exact form, written into and read from pinned arrays (`06` PRT-ENG-006).
- Values in the wire formats in base64url, unpadded, so JSON never escapes them, and a value's escaped string refused (`06` PRT-ENG-005, P-023).
