using barzap.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace barzap.Services {

    public class PacketHandler : BackgroundService {

        private readonly ILogger<PacketHandler> _Logger;
        private readonly PacketQueue _Queue;
        private readonly MatchManager _Match;
        private readonly Charger _Charger;
        private readonly UnitNames _UnitNames;

        public PacketHandler(ILogger<PacketHandler> logger, PacketQueue queue,
            MatchManager match, Charger charger,
            UnitNames unitNames) {

            _Logger = logger;
            _Queue = queue;
            _Match = match;
            _Charger = charger;
            _UnitNames = unitNames;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) {
            return Task.Run(async () => {
                _Logger.LogInformation($"started");

                while (stoppingToken.IsCancellationRequested == false) {
                    try {
                        Packet packet = await _Queue.Dequeue(stoppingToken);

                        _ProcessQueueEntry(packet, stoppingToken);
                    } catch (Exception ex) {
                        _Logger.LogError(ex, $"error in packet handler");
                    }
                }

                _Logger.LogInformation($"stopping");
            }, stoppingToken);
        }

        protected void _ProcessQueueEntry(Packet entry, CancellationToken cancel) {

            // hi - handshake
            if (entry.Op == "hi") {
                _Match.Get().TeamID = entry.ReadLong("t");
                _Logger.LogInformation($"handshake info set [team id={_Match.Get().TeamID}]");
            }

            // mk/rm - unit made, unit destroyed
            else if (entry.Op == "mk" || entry.Op == "rm") {
                BarUnit unit = BarUnit.Parse(entry);

                if (entry.Op == "mk") {
                    _Logger.LogDebug($"player gained unit [name={_UnitNames.GetUnitName(unit.DefID)}] [id={unit.ID}] [def id={unit.DefID}] "
                        + $"[metal cost={unit.MetalCost}] [energy cost={unit.EnergyCost}]");
                    _Match.Get().TotalMetal += unit.MetalCost;
                    _Match.Get().TotalEnergy += unit.EnergyCost;
                } else if (entry.Op == "rm") {

                    long? attackerTeamID = entry.ReadNullableLong("a");
                    _Logger.LogInformation($"unit destroyed {entry.Data} {attackerTeamID}");

                    decimal metalLost = unit.MetalCost / (decimal)_Match.Get().TotalMetal * 100m;
                    decimal energyLost = unit.EnergyCost / (decimal)_Match.Get().TotalEnergy * 100m;

                    if (attackerTeamID == _Match.Get().TeamID && unit.TeamID != _Match.Get().TeamID) {
                        _Logger.LogDebug($"player killed unit [name={_UnitNames.GetUnitName(unit.DefID)}] [metal %={metalLost:N2}] [energy %={energyLost:N2}] "
                            + $"[id={unit.ID}] [def id={unit.DefID}] [metal cost={unit.MetalCost}] [energy cost={unit.EnergyCost}]");

                        _Charger.RemoveCharge(metalLost + energyLost);

                    } else if (unit.TeamID == _Match.Get().TeamID) {
                        _Logger.LogDebug($"player lost unit [name={_UnitNames.GetUnitName(unit.DefID)}] [metal %={metalLost:N2}] [energy %={energyLost:N2}] "
                            + $"[id={unit.ID}] [def id={unit.DefID}] [metal cost={unit.MetalCost}] [energy cost={unit.EnergyCost}]");

                        _Charger.AddCharge(metalLost + energyLost);

                        _Match.Get().TotalMetal -= unit.MetalCost;
                        _Match.Get().TotalEnergy -= unit.EnergyCost;
                    }

                } else {
                    _Logger.LogWarning($"someone forgot to update the valid ops");
                }
            }

            else if (entry.Op == "dm") {
                _Logger.LogInformation(entry.Data);
            }
            
            // sm - set metal
            else if (entry.Op == "sm") {
                _Match.Get().TotalMetal = long.Parse(entry.Data);
                _Logger.LogDebug($"total metal set thru packet [value={_Match.Get().TotalMetal}]");
            }
            
            // se - set energy
            else if (entry.Op == "se") {
                _Match.Get().TotalEnergy= long.Parse(entry.Data);
                _Logger.LogDebug($"total energy set thru packet [value={_Match.Get().TotalEnergy}]");
            }
            
            // em/ee - eco metal / eco energy
            else if (entry.Op == "em" || entry.Op == "ee") {

                BarEco eco = new();
                eco.Current = entry.ReadDecimal("c");
                eco.Max = entry.ReadDecimal("s");
                eco.Pull = entry.ReadDecimal("p");
                eco.Income = entry.ReadDecimal("i");
                eco.Expense = entry.ReadDecimal("e");

                if (entry.Op == "em") {
                    _Match.Get().Metal = eco;
                } else if (entry.Op == "ee") {
                    _Match.Get().Energy = eco;
                }
            }

            // is - set unit name
            else if (entry.Op == "is") {
                _Logger.LogInformation($"given unit name [unitDefID={entry.GetField("i")}] [name={entry.GetField("n")}]");
                _UnitNames.SetUnitName(entry.ReadLong("i"), entry.GetField("n") ?? "<>");
            }
        }

    }
}
