using MediatR;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Printing.ReceiptLayout;

namespace Sugarista.Application.Printing.Commands.UpdateReceiptLayout;

/// <summary>
/// Replaces the receipt layout from the admin panel. The posted document is
/// merged over the defaults, validated (Persian errors on failure) and stored
/// as JSON on SiteSettings.
/// </summary>
public record UpdateReceiptLayoutCommand(ReceiptLayoutPatch Layout) : IRequest;
