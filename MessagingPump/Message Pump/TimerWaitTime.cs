//---------------------------------------------------------------------------------
// Copyright (c) 2013, Microsoft Corporation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//---------------------------------------------------------------------------------

using System;
using System.Threading;

namespace Microsoft.ServiceBus.Samples.MessagePump
{
    class TimerWaitTime
    {
        const long MinTimerWaitTimeInMilliseconds = 50;
        const long MaxTimerWaitTimeInMilliseconds = 60000;

        long waitTime;

        public TimerWaitTime()
        {
            this.waitTime = MinTimerWaitTimeInMilliseconds / 2;
        }

        // Initialize wait time to half of the miminum wait time because the Get() method always doubles the wait time.
        // This method is called when an EndXXX() call returns successfully.
        public void Reset()
        {
            lock (this)
            {
                this.waitTime = MinTimerWaitTimeInMilliseconds / 2;
            }
        }

        // Double wait time and return result.
        public long Get()
        {
            lock (this)
            {
                waitTime = Math.Min(waitTime * 2, MaxTimerWaitTimeInMilliseconds);
                return waitTime;
            }
        }
    }
}
