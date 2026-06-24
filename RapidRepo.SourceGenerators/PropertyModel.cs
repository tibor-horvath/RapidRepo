using System.Collections.Generic;

namespace RapidRepo.SourceGenerators;

internal sealed class PropertyModel
{
    public PropertyModel(string name, string typeDisplay, string implementation, IReadOnlyList<string> namespaces)
    {
        Name = name;
        TypeDisplay = typeDisplay;
        Implementation = implementation;
        Namespaces = namespaces;
    }

    public string Name { get; }
    public string TypeDisplay { get; }
    public string Implementation { get; }
    public IReadOnlyList<string> Namespaces { get; }
}
