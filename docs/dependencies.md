# Dependencies

Central versions in `Directory.Packages.props`.

| Package | Reason |
|---------|--------|
| Microsoft.NET.Test.Sdk | Test runner |
| xunit | Unit tests |
| xunit.runner.visualstudio | IDE/CI test discovery |
| coverlet.collector | Coverage (optional CI) |

Runtime uses built-in WPF, WinForms (`NotifyIcon` only), and BCL only for the application.
