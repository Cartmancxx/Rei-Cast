# Validation

## v0.2.0, 2026-10-01

The public source builds with the Windows .NET Framework compiler. All 39 native
self-tests passed. The suite covers local task reduction/tailing, snapshot limits/expiry,
pagination, finite wake recovery, display selection and geometry.

Software tests include arbitrary display sizes, saved monitor identity after
renumbering, exclusion of the primary monitor, ambiguous automatic selection,
JZFS adapter detection without a resolution filter, portrait/square layouts and
uniform scaling. Generated previews at 1024×600, 480×854 and 480×480 were inspected.

The installed v0.2.0 app recognized the original JZFS adapter, reported a fresh
visible-window heartbeat and retained the user's private avatar. A 20.01-second
sample on 16 logical processors averaged 0.0146% system CPU, a 65.86 MB working
set and 40.77 MB private memory. The short sample is not a peak guarantee and
excludes Codex, Dot, DeepCreative and driver overhead.

The v0.2.0 interactive scheduled action was exercised by stopping the installed
app and requesting a fresh start: a new process, current heartbeat and Task
Scheduler result 0 were verified. The launcher explicitly reads UTF-8; the old
Windows PowerShell default encoding could corrupt Chinese weather/status text
and falsely report a startup timeout. Actual reboot/logon remains unverified.

The original physical LT360 VISION setup exposes an 854×480 secondary monitor
through `JZFSDisplayDriver Device`. No other cooler model is available to the
maintainer for physical testing. Windows window visibility and a paint heartbeat
do not establish physical D-Cast transport health.

## Earlier startup and dialogue verification

A direct logon action pointing to a virtualized LocalAppData path failed with
file-not-found. Installing in the physical user folder made the interactive
current-user scheduled action start a new process with a fresh heartbeat and
Task Scheduler result 0. Actual reboot/logon validation remains pending.

The Dot snapshot producer was checked for initial creation, repeated atomic
replacement, Windows PowerShell 5.1 compatibility, visible demo mode, and expiry
back to task/standby. This verifies the local publisher and display consumer;
real Dot per-turn invocation remains a separate integration check.

A 22.35-second sample of the earlier 854×480 build used 0.140625 CPU seconds,
approximately 0.0393% average system CPU on a 16-logical-processor host, with a
62.25 MB working set and 41.12 MB private memory. This excludes Codex, Dot,
DeepCreative and driver overhead and is not a peak guarantee. The new layouts
preserve bounded sampling and a one-second UI timer; performance on other panel
sizes should be measured by contributors.
