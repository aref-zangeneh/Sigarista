namespace Sugarista.Domain.Entities;

public enum OrderChannel
{
    Online,
    Pos
}

public enum PosServiceType
{
    DineIn,
    TakeAway
}

public enum PosPaymentMethod
{
    Cash,
    Card,
    OnlineGateway,
    Complimentary,
    CardToCard,
    Split
}

public enum OrderKind
{
    Standard,
    InternalUse,
    Complimentary
}
