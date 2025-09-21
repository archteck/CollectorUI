namespace CollectorUI.Data;

public class SolutionReport
{
    public int Id { get; set; }

    // Caminho completo da solução (.slnx ou .sln)
    public string SolutionPath { get; set; } = default!;

    // Momento em que se gerou (ou atualizou) o report para esta solução
    public DateTime LastGeneratedAt { get; set; }
}
