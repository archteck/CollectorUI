using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace CollectorUI.ViewModels;

/// <summary>
/// Representa um nó de namespace na árvore de dependências, com suporte a seleção.
/// </summary>
public partial class NamespaceNodeViewModel : ObservableObject
{
    /// <summary>
    /// Nome completo do namespace.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Nós filhos (sub-namespaces).
    /// </summary>
    public ObservableCollection<NamespaceNodeViewModel> Children { get; } = new();

    [ObservableProperty]
    private bool _isChecked;

    /// <summary>
    /// Cria um novo nó de namespace.
    /// </summary>
    /// <param name="name">Nome do namespace.</param>
    public NamespaceNodeViewModel(string name) => Name = name;
}
