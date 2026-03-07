using CollectorUI.ViewModels;
using Xunit;

namespace CollectorUI.Tests;

public class NamespaceNodeViewModelTests
{
    [Fact(DisplayName = "DisplayName uses leaf segment for dotted namespaces")]
    public void Constructor_DottedNamespace_SetsLeafDisplayName()
    {
        var node = new NamespaceNodeViewModel("Acme.Product.Feature");

        Assert.Equal("Acme.Product.Feature", node.Name);
        Assert.Equal("Feature", node.DisplayName);
    }

    [Fact(DisplayName = "DisplayName equals name for root namespaces")]
    public void Constructor_RootNamespace_SetsDisplayNameToName()
    {
        var node = new NamespaceNodeViewModel("Acme");

        Assert.Equal("Acme", node.DisplayName);
    }
}
