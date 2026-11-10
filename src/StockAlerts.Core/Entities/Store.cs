namespace StockAlerts.Core.Entities;

public sealed class Store
{
    public Store(string name, string website)
    {
        Id = Guid.NewGuid();
        Name = name;
        Website = website;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Website { get; private set; }

    public void Update(string name, string website)
    {
        Name = name;
        Website = website;
    }
}
