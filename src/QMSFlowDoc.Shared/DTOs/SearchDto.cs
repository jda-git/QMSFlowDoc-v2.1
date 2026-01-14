using System;
using System.Collections.Generic;

namespace QMSFlowDoc.Shared.DTOs;

public record SearchResultDto(
    Guid Id,
    string EntityType, // DOCUMENT, REAGENT, EQUIPMENT
    string Title,
    string Subtitle,
    string Route // The tag or route to navigate to
);

public record SearchQueryRequest(
    string Query
);

