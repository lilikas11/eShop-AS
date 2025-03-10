namespace eShop.Ordering.Infrastructure.Repositories;
using System.Diagnostics;


public class OrderRepository
    : IOrderRepository
{
    private readonly OrderingContext _context;

    public IUnitOfWork UnitOfWork => _context;

    public OrderRepository(OrderingContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    private static readonly ActivitySource ActivitySource = new("Ordering.API");

    public Order Add(Order order)
    {

        using var activity = ActivitySource.StartActivity("Database - Insert Order");

        activity?.SetTag("db.system", "sql");
        activity?.SetTag("db.operation", "INSERT");
        activity?.SetTag("order.id", order.Id);
        
        return _context.Orders.Add(order).Entity;

    }

    public async Task<Order> GetAsync(int orderId)
    {
        var order = await _context.Orders.FindAsync(orderId);

        if (order != null)
        {
            await _context.Entry(order)
                .Collection(i => i.OrderItems).LoadAsync();
        }

        return order;
    }

    public void Update(Order order)
    {
        _context.Entry(order).State = EntityState.Modified;
    }
}
