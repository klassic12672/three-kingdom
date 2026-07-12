# Prompt Package: M0 External Evidence

Use only after a committed baseline exists and the user has authorized the required GitHub, CI, signing, or platform actions.

## Prompt

~~~text
/goal

Collect and record the remaining M0 evidence for commit <EXACT SHA>.

Read AGENTS.md, docs/ROADMAP.md, SP-00 through SP-02, docs/DEVELOPMENT.md,
docs/SIMULATION.md, docs/CONTENT_PIPELINE.md, docs/RELEASE.md, and CI/release workflows.

Use:
- build-release-engineer for CI, artifact, smoke, and signing evidence;
- verification-reviewer for read-only same-SHA and acceptance review.
The main agent owns external authorization checks and documentation status.

Verify before action:
- HEAD exists and matches the requested SHA;
- origin is the user-approved remote;
- the working tree is clean;
- LFS objects are complete;
- no credential or signing material is tracked.

Evidence stages:
1. Fresh clone: LFS pull/fsck, preflight with templates, Release tests, Godot import.
2. Hosted CI: macOS arm64 and Windows x64 jobs for the exact same SHA.
3. SP-01: matching ten-year/1,000-entity checksum on both hosted jobs.
4. SP-02: matching registry/load-order checksum and zero diagnostics on both hosted jobs.
5. Native smoke: architecture, embedded and sidecar manifest SHA, required log markers, clean exit.
6. Release signing: Developer ID signing/notarization/stapling and Windows Authenticode/timestamp verification, only when credentials and authorization are supplied.

For every stage record:
- commit SHA;
- runner or device OS and architecture;
- exact command;
- run URL and run ID when hosted;
- artifact name and SHA-256;
- key output or checksum;
- pass, fail, or unavailable;
- reason and next action when incomplete.

Do not retry by weakening scripts, bypassing signing gates, changing expected checksums, or using evidence from another revision.
Do not expose secrets in commands, logs, reports, or committed files.

Update statuses only to the strongest evidence demonstrated. Keep external or physical gates unchecked when unavailable.
~~~
