// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using Microsoft.AspNetCore.Antiforgery;

namespace Microsoft.AspNetCore.Http.Extensions;

internal sealed class AntiforgeryMetadata(bool required) : IAntiforgeryMetadata
{
    public bool Required { get; } = required;
}
