namespace StockAlerts.Core.Entities;

public sealed class Product
{
    private readonly List<Listing> _listings = [];

    public Product(string name, string category)
    {
        Id = Guid.NewGuid();
        Name = name;
        Category = category;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Category { get; private set; }
    public IReadOnlyCollection<Listing> Listings => _listings;

    public void Update(string name, string category)
    {
        Name = name;
        Category = category;
    }
}
