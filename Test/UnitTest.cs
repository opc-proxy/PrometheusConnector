using System;
using Xunit;
using OpcProxyClient;
using OpcProxyCore;
using opcPrometheus;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Test
{
    public class UnitTest
    {

        [Fact]
        public void Run()
        {
            string json_config = @"
            {
                'opcServerURL':'opc.tcp://localhost:4840/freeopcua/server/',
                'reconnectPeriod':10,
                'publishingInterval': 1000,
                'nodesDatabase':{
                'isInMemory':true
                },

                'loggerConfig' :{
                    'loglevel' :'debug'
                },

                'nodesLoader' : {
                    'targetIdentifier' : 'browseName',
                    'whiteList':['MyVariable','MyVariable2','MyVariable3']
                }
            }";

            var j = JObject.Parse(json_config);
            var s = new serviceManager(j);
            var prom = new opcPromConnect();
            s.addConnector(prom);
            s.run();
        }
    }
}
