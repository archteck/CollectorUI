# CollectorUI

CollectorUI is a cross‑platform desktop application built with .NET 9 and C# 13. Its goal is to generate and visualize code coverage reports from one or more coverage files. It leverages ReportGenerator (by Daniel Palme) to convert raw coverage data into human‑readable reports.

Repository: https://github.com/archteck/CollectorUI

## Overview

- Generate coverage reports from Cobertura.
- Produce clean, human‑readable outputs in HTML.
- Provide a UI‑first workflow to select coverage inputs and view results locally.

ReportGenerator supports a wide range of coverage formats (including Cobertura) and output types; see its docs for details [[1]](https://reportgenerator.io/) [[3]](https://github.com/danielpalme/ReportGenerator).

This project aims to merge steps in on easy simple click on UI ambient.

## Tech Stack

- .NET 9.0
- C# 13.0
- Avalonia UI

## Getting Started

1. Prerequisites
   - .NET 9.0 SDK
   - An IDE (e.g., JetBrains Rider, Visual Studio, VS Code)

2. Clone
   ```bash
   git clone https://github.com/archteck/CollectorUI.git
   cd CollectorUI
   ```

3. Restore
   ```bash
   dotnet restore
   ```

4. Build and Run
   ```bash
   dotnet build
   dotnet run
   ```
   (You can also launch from your IDE.)

## Using ReportGenerator

CollectorUI relies on ReportGenerator to transform coverage data into readable reports. ReportGenerator is distributed as a .NET global tool on NuGet and if not installed this project will install it for you.

- Install (global):
  ```bash
  dotnet tool install --global dotnet-reportgenerator-globaltool
  ```
  See the NuGet package for more details [[2]](https://www.nuget.org/packages/dotnet-reportgenerator-globaltool) and usage examples in the official docs [[1]](https://reportgenerator.io/usage).


## Typical Workflow

1. Produce coverage in your preferred format (e.g., by running tests with coverage enabled).
2. Use ReportGenerator to convert the raw coverage output into one or more readable formats.
3. Open and inspect the generated report output within the app or your browser.

## Contributing

Contributions are welcome!

1. Fork the repository.
2. Create a feature branch.
3. Make changes with tests where applicable.
4. Submit a detailed Pull Request.

## License

- CollectorUI is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
- ReportGenerator is a separate, third‑party tool by Daniel Palme.

## Legal and Attribution

- Ownership and attribution: CollectorUI is not affiliated with or endorsed by ReportGenerator or its authors. “ReportGenerator” is a third‑party project by Daniel Palme [[3]](https://github.com/danielpalme/ReportGenerator).
- Legality of usage: Installing and invoking ReportGenerator as a global .NET tool from your development environment or CI is permitted under its open‑source license. You should comply with ReportGenerator’s licensing terms (Apache License) when redistributing or bundling it; simply using it as an external tool (installed from NuGet) is a typical and permitted usage [[1]](https://reportgenerator.io/) [[2]](https://www.nuget.org/packages/dotnet-reportgenerator-globaltool).
- This repository does not claim ownership of ReportGenerator and does not redistribute its binaries; it instructs users to install the tool from official sources.

## Acknowledgements

- ReportGenerator by Daniel Palme [[3]](https://github.com/danielpalme/ReportGenerator).
- The broader .NET and open‑source community.
