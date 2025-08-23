using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

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
    /// Nó pai (null para raiz).
    /// </summary>
    public NamespaceNodeViewModel? Parent { get; set; }

    /// <summary>
    /// Nós filhos (sub-namespaces).
    /// </summary>
    public ObservableCollection<NamespaceNodeViewModel> Children { get; } = new();

    [ObservableProperty]
    private bool _isChecked = true;

    [ObservableProperty]
    private bool _isExpanded = true;

    // Evita cascata para filhos quando o estado é ajustado a partir de um filho.
    private bool _suppressChildCascade;

    partial void OnIsCheckedChanged(bool value)
    {
        // Se estamos a ajustar o estado do pai em função dos filhos, não propagar para os filhos.
        if (_suppressChildCascade)
        {
            _suppressChildCascade = false;
            return;
        }

        // Propaga para todos os descendentes.
        foreach (var child in Children)
        {
            child.IsChecked = value;
        }

        // Atualiza os pais com base no estado dos filhos:
        // - sem tri-state: pai fica checked se ALGUM filho estiver checked; fica unchecked se NENHUM estiver checked.
        UpdateAncestorsFromChildren();
    }

    private void UpdateAncestorsFromChildren()
    {
        var p = Parent;
        while (p is not null)
        {
            var anyChecked = p.Children.Any(c => c.IsChecked);
            var desired = anyChecked;

            if (p.IsChecked != desired)
            {
                p._suppressChildCascade = true;
                p.IsChecked = desired;
            }

            p = p.Parent;
        }
    }

    /// <summary>
    /// Cria um novo nó de namespace.
    /// </summary>
    /// <param name="name">Nome do namespace.</param>
    public NamespaceNodeViewModel(string name) => Name = name;
}
