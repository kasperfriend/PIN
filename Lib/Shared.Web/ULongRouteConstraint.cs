using System;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Shared.Web;

/// <summary>
///     The inline route constraint <c>{param:ulong}</c>. ASP.NET Core routing does not ship
///     one (its built-in integer constraints stop at <c>int</c> and <c>long</c>), so a
///     controller template like <c>api/v3/characters/{characterId:ulong}</c> fails with
///     <c>The constraint reference 'ulong' could not be resolved to a type</c> when the
///     endpoint matcher is built — lazily, on the host's first request — and every request
///     to that host dies in a 500. <see cref="BaseWebServer"/> registers this class for
///     every web host through <c>RouteOptions.ConstraintMap</c> so the constraint behaves
///     like its framework siblings: the parameter only matches when it is (or parses as,
///     invariant-culture) an unsigned 64-bit integer.
/// </summary>
public sealed class ULongRouteConstraint : IRouteConstraint
{
    public bool Match(HttpContext httpContext, IRouter route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (values.TryGetValue(routeKey, out var value))
        {
            if (value is ulong)
            {
                return true;
            }

            var valueString = Convert.ToString(value, CultureInfo.InvariantCulture);
            return ulong.TryParse(valueString, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }

        return false;
    }
}
