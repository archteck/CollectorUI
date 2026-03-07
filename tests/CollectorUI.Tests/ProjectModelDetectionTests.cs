using CollectorUI.Models;
using Xunit;

namespace CollectorUI.Tests;

public sealed class ProjectModelDetectionTests
{
    [Fact(DisplayName = "Test SDK project is detected as test project without coverlet")]
    public void FromProjectFile_TestSdkOnlyProject_SetsIsTestProjectTrue()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectPath = Path.Combine(tempRoot, "Sample.Tests.csproj");
            File.WriteAllText(projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.3.0" />
                    <PackageReference Include="xunit.v3" Version="3.2.2" />
                  </ItemGroup>
                </Project>
                """);

            var model = ProjectModel.FromProjectFile(projectPath);

            Assert.True(model.IsTestProject);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact(DisplayName = "Non-test project remains non-test")]
    public void FromProjectFile_NoTestPackages_SetsIsTestProjectFalse()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var projectPath = Path.Combine(tempRoot, "Sample.App.csproj");
            File.WriteAllText(projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Serilog" Version="4.3.1" />
                  </ItemGroup>
                </Project>
                """);

            var model = ProjectModel.FromProjectFile(projectPath);

            Assert.False(model.IsTestProject);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
