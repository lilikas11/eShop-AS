namespace eShop.Ordering.API.Application.Commands;

using System.Diagnostics;
using OpenTelemetry.Trace;
using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;

// Regular CommandHandler
public class CreateOrderCommandHandler
    : IRequestHandler<CreateOrderCommand, bool>
{
    private static readonly ActivitySource ActivitySource = new("Ordering.API");

    private readonly IOrderRepository _orderRepository;
    private readonly IIdentityService _identityService;
    private readonly IMediator _mediator;
    private readonly IOrderingIntegrationEventService _orderingIntegrationEventService;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    // Using DI to inject infrastructure persistence Repositories
    public CreateOrderCommandHandler(IMediator mediator,
        IOrderingIntegrationEventService orderingIntegrationEventService,
        IOrderRepository orderRepository,
        IIdentityService identityService,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _orderingIntegrationEventService = orderingIntegrationEventService ?? throw new ArgumentNullException(nameof(orderingIntegrationEventService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    }

    private static string MaskCardNumber(string cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 4)
            return "****";

        return new string('*', cardNumber.Length - 4) + cardNumber[^4..];
    }


    public async Task<bool> Handle(CreateOrderCommand message, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("CQRS - Handle CreateOrderCommand");

        activity?.SetTag("user.id", message.UserId);
        activity?.SetTag("user.name", message.UserName);

        activity?.SetTag("order.total_items", message.OrderItems?.Count() ?? 0);
        activity?.SetTag("order.total_value", (message.OrderItems?.Sum(i => i.UnitPrice * i.Units) ?? 0).ToString("F2"));

        activity?.SetTag("order.payment.method", message.CardTypeId);
        activity?.SetTag("order.card_number", MaskCardNumber(message.CardNumber)); // Número do cartão mascarado

        activity?.SetTag("order.shipping.city", message.City);
        activity?.SetTag("order.shipping.country", message.Country);
        activity?.SetTag("order.shipping.state", message.State);
        activity?.SetTag("order.shipping.zipcode", message.ZipCode);


        _logger.LogInformation("Processing new order - Order: {@Order}", message);

        // Add Integration event to clean the basket
        var orderStartedIntegrationEvent = new OrderStartedIntegrationEvent(message.UserId);
        await _orderingIntegrationEventService.AddAndSaveEventAsync(orderStartedIntegrationEvent);

        var address = new Address(message.Street, message.City, message.State, message.Country, message.ZipCode);
        var order = new Order(message.UserId, message.UserName, address, message.CardTypeId, message.CardNumber, message.CardSecurityNumber, message.CardHolderName, message.CardExpiration);

        foreach (var item in message.OrderItems)
        {
            order.AddOrderItem(item.ProductId, item.ProductName, item.UnitPrice, item.Discount, item.PictureUrl, item.Units);
        }

        _logger.LogInformation("Creating Order - Order: {@Order}", order);

        _orderRepository.Add(order);

        var result = await _orderRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        if (!result)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", "Failed to save order in the database");
            _logger.LogError("Failed to save order in the database for user {UserId}", message.UserId);        
        }
        else
        {
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("error", false);
            _logger.LogInformation("Order successfully created for user {UserId}", message.UserId);

        }


        return result;
    }
}

// Use for Idempotency in Command process
public class CreateOrderIdentifiedCommandHandler : IdentifiedCommandHandler<CreateOrderCommand, bool>
{
    public CreateOrderIdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<CreateOrderCommand, bool>> logger)
        : base(mediator, requestManager, logger)
    {
    }

    protected override bool CreateResultForDuplicateRequest()
    {
        return true; // Ignore duplicate requests for creating order.
    }
}
