﻿/*  Copyright (C) 2014 Colton Manville
    This file is part of CSharpMiner.

    CSharpMiner is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    CSharpMiner is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with CSharpMiner.  If not, see <http://www.gnu.org/licenses/>.*/

using CSharpMiner.ModuleLoading;
using CSharpMiner.Pools;
using CSharpMiner.Stratum;
using MiningDevice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace DeviceManager
{
    [DataContract]
    [MiningModule(Description = "Uses the stratum protocol to generate a unique work item for each device it manages. It will allow the device to continue working on its work item until the device requests a new one, the server forces a work restart, or the device submits a stale share.")]
    public class IndividualWorkManager : WorkManagerBase
    {
        private static Random _random = null;
        private static Random Random
        {
            get
            {
                if(_random == null)
                {
                    _random = new Random();
                }

                return _random;
            }
        }

        protected override void SetUpDevice(IMiningDevice d)
        {
            double fullHashTimeSec = Int32.MaxValue / d.HashRate; // Hashes devided by Hashes per second yeilds seconds
            double safeWaitTime = fullHashTimeSec * 0.85 * 0.95; // Assume we lose 15% of our hash rate just in case then only wait until we've covered 95% of the hash space
            d.WorkRequestTimer.Interval = safeWaitTime;
        }

        private int startingNonce = 0;

        protected override void StartWork(IPoolWork work, IMiningDevice device, bool restartAll, bool requested)
        {
            StratumWork stratumWork = work as StratumWork;

            if (stratumWork != null)
            {
                startingNonce = Random.Next();
                StartWorkOnDevice(stratumWork, device, restartAll, requested);
            }
        }

        protected override void NoWork(IPoolWork oldWork, IMiningDevice device, bool requested)
        {
            StratumWork stratumWork = oldWork as StratumWork;

            if (stratumWork != null)
            {
                StartWorkOnDevice(stratumWork, device, false, requested);
            }
        }

        private void StartWorkOnDevice(StratumWork work, IMiningDevice device, bool restartWork, bool requested)
        {
            if (!restartWork && device != null && requested)
            {
                StartWorkOnDevice(device, work.CommandArray, work.Extranonce1, work.Diff);
            }
            else if(restartWork)
            {
                StartWorking(work.CommandArray, work.Extranonce1, work.Diff);
            }
        }

        private void StartWorking(object[] param, string extranonce1, int diff)
        {
            foreach (IMiningDevice device in this.loadedDevices)
            {
                StartWorkOnDevice(device, param, extranonce1, diff);
            }
        }

        private void StartWorkOnDevice(IMiningDevice device, object[] param, string extranonce1, int diff)
        {
            device.StartWork(new StratumWork(param, extranonce1, string.Format("{0,8:X8}", startingNonce), diff));
            if (startingNonce != int.MaxValue)
            {
                startingNonce++;
            }
            else
            {
                startingNonce = 0;
            }
        }
    }
}
