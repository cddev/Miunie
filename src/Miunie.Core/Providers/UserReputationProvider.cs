// This file is part of Miunie.
//
//  Miunie is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  Miunie is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with Miunie. If not, see <https://www.gnu.org/licenses/>.

using Miunie.Core.Entities;
using Miunie.Core.Entities.Discord;
using Miunie.Core.Infrastructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Miunie.Core.Providers
{
    public class UserReputationProvider : IUserReputationProvider
    {
        private readonly IMiunieUserProvider _userProvider;
        private readonly IDateTime _dateTime;

        public UserReputationProvider(IMiunieUserProvider userProvider, IDateTime dateTime)
        {
            _userProvider = userProvider;
            _dateTime = dateTime;
        }

        public int TimeoutInSeconds { get; } = 1800;

        public IEnumerable<ReputationEntry> GetReputation(MiunieUser invoker)
        {
            var rep = new List<ReputationEntry>();

            if (invoker is null || invoker.Reputation is null) { return rep; }

            foreach (MiunieUser user in _userProvider.GetAllUsers().Where(x => x.Id != invoker.Id))
            {
                if (user.Reputation.PlusRepLog.ContainsKey(invoker.UserId))
                {
                    rep.Add(new ReputationEntry(user.UserId, user.Name, user.Reputation.PlusRepLog[invoker.UserId], ReputationType.Plus, true));
                }

                if (user.Reputation.MinusRepLog.ContainsKey(invoker.UserId))
                {
                    rep.Add(new ReputationEntry(user.UserId, user.Name, user.Reputation.PlusRepLog[invoker.UserId], ReputationType.Minus, true));
                }
            }

            foreach (KeyValuePair<ulong, DateTime> entry in invoker.Reputation.PlusRepLog)
            {
                var user = _userProvider.GetById(entry.Key, invoker.GuildId);
                rep.Add(new ReputationEntry(user.UserId, user.Name, entry.Value, ReputationType.Plus));
            }

            foreach (KeyValuePair<ulong, DateTime> entry in invoker.Reputation.MinusRepLog)
            {
                var user = _userProvider.GetById(entry.Key, invoker.GuildId);
                rep.Add(new ReputationEntry(user.UserId, user.Name, entry.Value, ReputationType.Minus));
            }

            return rep.OrderByDescending(x => x.GivenAt);
        }

        public void AddReputation(MiunieUser invoker, MiunieUser target)
        {
            target.Reputation.Value++;
            _ = target.Reputation.PlusRepLog.TryAdd(invoker.UserId, _dateTime.UtcNow);
            _userProvider.StoreUser(target);
        }

        public void RemoveReputation(MiunieUser invoker, MiunieUser target)
        {
            target.Reputation.Value--;
            _ = target.Reputation.MinusRepLog.TryAdd(invoker.UserId, _dateTime.UtcNow);
            _userProvider.StoreUser(target);
        }

        public bool CanAddReputation(MiunieUser invoker, MiunieUser target)
            => HasTimeout(target.Reputation.PlusRepLog, invoker);

        public bool CanRemoveReputation(MiunieUser invoker, MiunieUser target)
            => HasTimeout(target.Reputation.MinusRepLog, invoker);

        private bool HasTimeout(ConcurrentDictionary<ulong, DateTime> log, MiunieUser invoker)
        {
            _ = log.TryGetValue(invoker.UserId, out var lastRepDateTime);

            if ((_dateTime.UtcNow - lastRepDateTime).TotalSeconds <= TimeoutInSeconds) { return true; }

            _ = log.TryRemove(invoker.UserId, out _);
            _userProvider.StoreUser(invoker);
            return false;
        }
    }
}
