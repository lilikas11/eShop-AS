namespace eShop.Ordering.API.Application.Commands;

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;
using MediatR;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

public class SetPaidOrderStatusCommandHandler : IRequestHandler<SetPaidOrderStatusCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<SetPaidOrderStatusCommandHandler> _logger;
    private static readonly ActivitySource ActivitySource = new("Ordering.API");

    /// <summary>
    /// Handler which processes the command when
    /// Shipment service confirms the payment
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>

    public SetPaidOrderStatusCommandHandler(IOrderRepository orderRepository, ILogger<SetPaidOrderStatusCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<bool> Handle(SetPaidOrderStatusCommand command, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("CQRS - Set Paid Order Status");

        activity?.SetTag("order.id", command.OrderNumber);
        activity?.SetTag("order.status", "Paid");

        _logger.LogInformation("Setting order {OrderId} as PAID.", command.OrderNumber);

        // Simula um atraso no processamento (simulação de validação de pagamento)
        await Task.Delay(10000, cancellationToken);

        var orderToUpdate = await _orderRepository.GetAsync(command.OrderNumber);
        if (orderToUpdate == null)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            _logger.LogError("Order {OrderId} not found when setting status to PAID.", command.OrderNumber);
            return false;
        }

        orderToUpdate.SetPaidStatus();
        var result = await _orderRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        if (!result)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            _logger.LogError("Failed to update order {OrderId} to PAID.", command.OrderNumber);
        }
        else
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
            _logger.LogInformation("Order {OrderId} successfully updated to PAID.", command.OrderNumber);
        }

        return result;
    }
}



// Use for Idempotency in Command process
public class SetPaidIdentifiedOrderStatusCommandHandler : IdentifiedCommandHandler<SetPaidOrderStatusCommand, bool>
{
    public SetPaidIdentifiedOrderStatusCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<SetPaidOrderStatusCommand, bool>> logger)
        : base(mediator, requestManager, logger)
    {
    }

    protected override bool CreateResultForDuplicateRequest()
    {
        return true; // Ignore duplicate requests for processing order.
    }
}
