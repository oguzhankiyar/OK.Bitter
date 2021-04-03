﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OK.Bitter.Common.Entities;
using OK.Bitter.Core.Managers;
using OK.Bitter.Core.Repositories;
using OK.GramHook;

namespace OK.Bitter.Api.Commands
{
    [Command("subscriptions")]
    public class SubscriptionCommand : BaseCommand
    {
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly ISymbolRepository _symbolRepository;
        private readonly ISocketServiceManager _socketServiceManager;

        public SubscriptionCommand(
            ISubscriptionRepository subscriptionRepository,
            ISymbolRepository symbolRepository,
            ISocketServiceManager socketServiceManager,
            IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
            _symbolRepository = symbolRepository ?? throw new ArgumentNullException(nameof(symbolRepository));
            _socketServiceManager = socketServiceManager ?? throw new ArgumentNullException(nameof(socketServiceManager));
        }

        public override async Task OnPreExecutionAsync()
        {
            await base.OnPreExecutionAsync();

            if (User == null)
            {
                await ReplyAsync("Unauthorized!");

                await AbortAsync();
            }
        }

        [CommandCase("get", "{symbol}")]
        public async Task GetAsync(string symbol)
        {
            if (symbol == "all")
            {
                var subscriptions = _subscriptionRepository.GetList(x => x.UserId == User.Id);

                var lines = new List<string>();

                foreach (var item in subscriptions)
                {
                    var sym = _symbolRepository.Get(x => x.Id == item.SymbolId);

                    lines.Add($"{sym.FriendlyName} for minimum {(item.MinimumChange * 100).ToString("0.00")}% change");
                }

                if (!lines.Any())
                {
                    await ReplyAsync("There are no subscriptions!");

                    return;
                }

                lines = lines.OrderBy(x => x).ToList();

                var skip = 0;
                var take = 25;

                while (skip < lines.Count)
                {
                    var items = lines.Skip(skip).Take(take);
                    await ReplyAsync(string.Join("\r\n", items));
                    await Task.Delay(500);

                    skip += take;
                }

                return;
            }
            else
            {
                var symbolEntity = _symbolRepository.Get(x => x.Name == symbol.ToUpperInvariant() || x.FriendlyName == symbol.ToUpperInvariant());
                if (symbolEntity == null)
                {
                    await ReplyAsync("Symbol is not found!");

                    return;
                }

                var subscription = _subscriptionRepository.Get(x => x.UserId == User.Id && x.SymbolId == symbolEntity.Id);
                if (subscription == null)
                {
                    await ReplyAsync("Subscription is not found!");

                    return;
                }

                await ReplyAsync($"{symbolEntity.FriendlyName} for minimum {(subscription.MinimumChange * 100).ToString("0.00")}% change\r\n");
            }
        }

        [CommandCase("set", "{symbol}", "{treshold}")]
        public async Task SetAsync(string symbol, string treshold)
        {
            if (!decimal.TryParse(treshold, out decimal tresholdValue))
            {
                await ReplyAsync("Invalid arguments!");

                return;
            }

            var minimumChange = tresholdValue / 100;

            if (symbol == "all")
            {
                var symbols = _symbolRepository.GetList();

                foreach (var symbolEntity in symbols)
                {
                    var subscription = _subscriptionRepository.Get(x => x.UserId == User.Id && x.SymbolId == symbolEntity.Id);
                    if (subscription == null)
                    {
                        _subscriptionRepository.Save(new SubscriptionEntity()
                        {
                            UserId = User.Id,
                            SymbolId = symbolEntity.Id,
                            MinimumChange = minimumChange
                        });
                    }
                    else
                    {
                        subscription.MinimumChange = minimumChange;

                        _subscriptionRepository.Save(subscription);
                    }
                }
            }
            else
            {
                var symbolEntity = _symbolRepository.Get(x => x.Name == symbol.ToUpperInvariant() || x.FriendlyName == symbol.ToUpperInvariant());
                if (symbolEntity == null)
                {
                    await ReplyAsync("Symbol is not found!");

                    return;
                }

                if (minimumChange < symbolEntity.MinimumChange)
                {
                    await ReplyAsync($"Minimum Change should be at least {(symbolEntity.MinimumChange * 100).ToString("0.00")}!");

                    return;
                }

                var subscription = _subscriptionRepository.Get(x => x.UserId == User.Id && x.SymbolId == symbolEntity.Id);
                if (subscription == null)
                {
                    _subscriptionRepository.Save(new SubscriptionEntity()
                    {
                        UserId = User.Id,
                        SymbolId = symbolEntity.Id,
                        MinimumChange = minimumChange
                    });
                }
                else
                {
                    subscription.MinimumChange = minimumChange;

                    _subscriptionRepository.Save(subscription);
                }
            }

            _socketServiceManager.UpdateSubscription(User.Id);

            await ReplyAsync("Success!");
        }

        [CommandCase("del", "{symbol}")]
        public async Task DelAsync(string symbol)
        {
            if (symbol == "all")
            {
                var subscriptions = _subscriptionRepository.GetList(x => x.UserId == User.Id);

                foreach (var subscription in subscriptions)
                {
                    _subscriptionRepository.Delete(subscription.Id);
                }
            }
            else
            {
                var symbolEntity = _symbolRepository.Get(x => x.Name == symbol.ToUpperInvariant() || x.FriendlyName == symbol.ToUpperInvariant());
                if (symbolEntity == null)
                {
                    await ReplyAsync("Symbol is not found!");

                    return;
                }

                var subscription = _subscriptionRepository.Get(x => x.UserId == User.Id && x.SymbolId == symbolEntity.Id);
                if (subscription == null)
                {
                    await ReplyAsync("Subscription is not found!");

                    return;
                }

                _subscriptionRepository.Delete(subscription.Id);
            }

            _socketServiceManager.UpdateSubscription(User.Id);

            await ReplyAsync("Success!");
        }
    }
}