using MediatR;
using Sugarista.Application.Printing.ReceiptLayout;

namespace Sugarista.Application.Printing.Queries.GetReceiptLayout;

/// <summary>Reads the resolved receipt layout (stored document or defaults).</summary>
public record GetReceiptLayoutQuery : IRequest<ResolvedReceiptLayout>;
