namespace CollectorUI.Data;

public class NamespaceSelection
{
    public int Id { get; set; }

    // Caminho completo da solução (.slnx)
    public string SolutionPath { get; set; } = default!;

    // Caminho completo do projeto (.csproj)
    public string ProjectPath { get; set; } = default!;

    // Nome completo do namespace
    public string Namespace { get; set; } = default!;

    // Estado do check; por ora só gravamos false, mas mantemos o campo para extensibilidade
    public bool IsChecked { get; set; }

    public DateTime SavedAt { get; set; }
}
