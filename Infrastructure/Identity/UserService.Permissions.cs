﻿using Application.Common.Caching;
using Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.Authorization;

namespace Infrastructure.Identity
{
    internal partial class UserService
    {
        public async Task<List<string>> GetPermissionsAsync(string userId, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(userId);

            _ = user ?? throw new NotFoundException(_localizer["User Not Found."]);

            var userRoles = await _userManager.GetRolesAsync(user);
            var permissions = new List<string>();
            foreach (var role in await _roleManager.Roles
                .Where(r => userRoles.Contains(r.Name))
                .ToListAsync(cancellationToken))
            {
                permissions.AddRange(await _db.RoleClaims
                    .Where(rc => rc.RoleId == role.Id && rc.ClaimType == Claims.Permission)
                    .Select(rc => rc.ClaimValue)
                    .ToListAsync(cancellationToken));
            }

            return permissions.Distinct().ToList();
        }

        public async Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken)
        {
            var permissions = await _cache.GetOrSetAsync(
                _cacheKeys.GetCacheKey(Claims.Permission, userId),
                () => GetPermissionsAsync(userId, cancellationToken),
                cancellationToken: cancellationToken);

            return permissions?.Contains(permission) ?? false;
        }

        public Task InvalidatePermissionCacheAsync(string userId, CancellationToken cancellationToken) =>
            _cache.RemoveAsync(_cacheKeys.GetCacheKey(Claims.Permission, userId), cancellationToken);
    }
}
