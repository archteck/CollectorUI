# CollectorUI

CollectorUI is a cross‑platform desktop application (Windows, macOS, and Linux) built with .NET 9 and C# 13. It simplifies generating and viewing code coverage reports by converting raw coverage outputs into readable HTML via ReportGenerator.

<img width="835" height="914" alt="image" src="https://github.com/user-attachments/assets/ed471e74-031c-43df-b5e7-a1aa42a2495f" />

## Features

- Coverage report generation (e.g., Cobertura) to HTML using ReportGenerator.
- Simple UI to select a solution and test projects.
- Hierarchical TreeDataGrid with:
  - Namespace selection via checkboxes.
  - Instant text filtering (matches nodes or any of their descendants).
  - Expansion state preservation when filtering and updating.
- Local persistence (SQLite + EF Core):
  - Saves per solution/project the deselected namespaces (default is selected).
  - Saves window size on close and restores it on next launch.
- Open the generated HTML report directly from the app.

## Tech Stack

- .NET 9.0
- C# 13.0
- Avalonia UI (cross‑platform desktop)
- SQLite + EF Core (lightweight local persistence)
- ReportGenerator (coverage to HTML conversion)

## Requirements

- .NET 9 SDK
- Your preferred IDE (Rider, Visual Studio, VS Code)
- Optional: ReportGenerator installed as a .NET global tool (the app will attempt to install it if needed)
  ```bash
  dotnet tool install --global dotnet-reportgenerator-globaltool
  ```

## Quick Start

1. Clone the repository
   ```bash
   git clone https://github.com/archteck/CollectorUI.git
   cd CollectorUI
   ```

2. Restore dependencies
   ```bash
   dotnet restore
   ```

3. Build and run
   ```bash
   dotnet build
   dotnet run
   ```
   (You can also launch directly from your IDE.)

## How to Use

1. Select a solution (.slnx)
   - Click “Select Solution” and point to a `.slnx` file.
   - The app detects and lists test projects.

2. Choose projects
   - Use “Select All” / “Unselect All” as needed.
   - Each tab represents one test project.

3. Explore namespaces
   - The TreeDataGrid shows the namespace hierarchy for the project (or dependencies when applicable).
   - Check/uncheck nodes to include/exclude areas in the report.
   - Use the text filter to search by namespace name. Nodes that match (or have matching descendants) remain visible.
   - Expansion state is preserved; with an active filter, relevant nodes are expanded to reveal matches.

4. Generate a report
   - Click “Generate Report”.
   - When completed, the “Open Report” button becomes available per project; click to open the HTML report in your browser.

5. Automatic persistence
   - When a report is generated, the project’s deselected namespaces are saved per solution/project.
   - Reopening the same solution restores those deselections (default is selected if no record exists).
   - On app close, the window size is saved and restored on next launch.

## Data Location

- A SQLite database is created under the user’s local application data folder, in a directory named “CollectorUI”:
  - Database file: `collectorui.sqlite`
- The schema includes:
  - A table for namespace selections per solution/project (stores deselections).
  - An app settings table (e.g., `Window.Width`, `Window.Height`).

## Troubleshooting

- ReportGenerator
  - If generation fails due to a missing tool, install it manually:
    ```bash
    dotnet tool install --global dotnet-reportgenerator-globaltool
    ```
  - Docs and usage: https://reportgenerator.io/

- Filter shows no results
  - Check the search text (case‑insensitive).
  - Clear the filter to return to the full view.

- Unexpected window size
  - A minimum size (600x600) is enforced for UX.
  - If closing while maximized, the last “Normal” size is saved and restored.

## Roadmap (Ideas)

- Export/import of selection preferences per solution.
- Support for additional ReportGenerator output formats.
- Theme/color options and keyboard shortcuts.
- CI/CD integration to pull reports automatically.

## Contributing

Contributions are welcome!

1. Fork the repository.
2. Create a feature branch.
3. Implement changes with tests where applicable.
4. Submit a Pull Request with a clear description of the goal and approach.

## License

- CollectorUI is licensed under the MIT License. See the [LICENSE](LICENSE) file.
- ReportGenerator is a third‑party project by Daniel Palme (separate license).

## Legal and Attribution

- Ownership and attribution: CollectorUI is not affiliated with or endorsed by ReportGenerator or its authors. “ReportGenerator” is a third‑party project by Daniel Palme [[3]](https://github.com/danielpalme/ReportGenerator).
- Legality of usage: Installing and invoking ReportGenerator as a global .NET tool from your development environment or CI is permitted under its open‑source license. You should comply with ReportGenerator’s licensing terms (Apache License) when redistributing or bundling it; simply using it as an external tool (installed from NuGet) is a typical and permitted usage [[1]](https://reportgenerator.io/) [[2]](https://www.nuget.org/packages/dotnet-reportgenerator-globaltool).
- This repository does not claim ownership of ReportGenerator and does not redistribute its binaries; it instructs users to install the tool from official sources.

## Acknowledgements

- ReportGenerator by Daniel Palme: https://github.com/danielpalme/ReportGenerator
- The .NET and open‑source community.
