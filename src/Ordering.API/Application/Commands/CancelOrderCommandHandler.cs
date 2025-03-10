namespace eShop.Ordering.API.Application.Commands;

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;
using MediatR;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<CancelOrderCommandHandler> _logger;
    private static readonly ActivitySource ActivitySource = new("Ordering.API");

    /// <summary>
    /// Handler which processes the command when
    /// customer executes cancel order from app
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    
    public CancelOrderCommandHandler(IOrderRepository orderRepository, ILogger<CancelOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<bool> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("CQRS - Cancel Order");

        activity?.SetTag("order.id", command.OrderNumber);
        activity?.SetTag("order.status", "Cancelled");

        _logger.LogInformation("Cancelling order {OrderId}.", command.OrderNumber);

        var orderToUpdate = await _orderRepository.GetAsync(command.OrderNumber);
        if (orderToUpdate == null)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            _logger.LogError("Order {OrderId} not found when attempting to cancel.", command.OrderNumber);
            return false;
        }

        orderToUpdate.SetCancelledStatus();
        var result = await _orderRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        if (!result)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            _logger.LogError("Failed to update order {OrderId} to CANCELLED.", command.OrderNumber);
        }
        else
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Order {OrderId} successfully updated to CANCELLED.", command.OrderNumber);
        }

        return result;
    }
}



// Use for Idempotency in Command process
public class CancelOrderIdentifiedCommandHandler : IdentifiedCommandHandler<CancelOrderCommand, bool>
{
    public CancelOrderIdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<CancelOrderCommand, bool>> logger)
        : base(mediator, requestManager, logger)
    {
    }

    protected override bool CreateResultForDuplicateRequest()
    {
        return true; // Ignore duplicate requests for processing order.
    }
}
