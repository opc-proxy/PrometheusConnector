using System;
using System.Threading;
using Newtonsoft.Json.Linq;
using OpcProxyClient;
using OpcProxyCore;
using NLog;
using Opc.Ua;
using Prometheus;
using System.Collections.Generic;
using System.Timers;
using converter;

namespace opcPrometheus
{
    public class opcPromConnect : IOPCconnect
    {
        serviceManager _serv;
        public KestrelMetricServer server;
        public static Logger logger = LogManager.GetCurrentClassLogger();
        public Dictionary<string, Gauge> variablesMap;

        public opcPromConfig _config;
        private static System.Timers.Timer aTimer;

        private NodesSelector selector;

        public void init(JObject config, CancellationTokenSource cts)
        {
            _config = config.ToObject<opcPromConfigWrapper>().prometheus;
            server = new KestrelMetricServer(_config.port);
            variablesMap = new Dictionary<string, Gauge>();
            var db_nodes = _serv.db.getDbNodes();
            try{
                selector = new NodesSelector(_config.variableFilter);
            }
            catch (Exception e){
                logger.Error("HTTP server initialization failed: " + e.Message);
                cts.Cancel();
            }
            if(!cts.IsCancellationRequested){
                foreach (var node in db_nodes)
                {
                    if(selector.selectNode(node.name)) variablesMap.Add(node.name, createMetric(node.name));
                }

                variablesMap.Add("opc_server_up", createMetric("opc_server_up","Describe the status of connection with the opc-server, 0 means TCP connection down."));

                setTimer();

                try
                {
                    server.Start();
                    logger.Info("Prometheus metrics exposed at http://localhost:"+_config.port+"/metrics");
                }
                catch (Exception e)
                {
                    logger.Error("HTTP server initialization failed: " + e.Message);
                    cts.Cancel();
                }
            }
        }

        public Gauge createMetric(string name, string message = "Metrics from OPC-Proxy client.")
        {
            if (!String.IsNullOrEmpty(_config.systemLabelName) && !String.IsNullOrEmpty(_config.systemLabelValue) )
                return Metrics.CreateGauge(name, message, new string[] { _config.systemLabelName });
            else return Metrics.CreateGauge(name, message);
        }

        public void setMetric(string name, double value)
        {
            try
            {
                if (!String.IsNullOrEmpty(_config.systemLabelName) && !String.IsNullOrEmpty(_config.systemLabelValue) )
                    variablesMap.GetValueOrDefault(name, null)?.WithLabels(new string[] { _config.systemLabelValue }).Set(value);
                else variablesMap.GetValueOrDefault(name, null)?.Set(value);
                logger.Debug("Set variable name {0} to val {1}", name, value.ToString());
            }
            catch (Exception e)
            {
                logger.Error("Failed metric update for variable: " + name);
                logger.Error("Error details: " + e.Message);
            }
        }

        private void setTimer()
        {
            aTimer = new System.Timers.Timer(2000);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += server_connection_metric;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }
        void server_connection_metric(Object source, ElapsedEventArgs e)
        {
            double v = _serv.opc.isConnected() ? 1 : 0;
            setMetric("opc_server_up",v);
        }

        public void clean()
        {
            server?.Stop();
            aTimer?.Stop();
            aTimer?.Dispose();
        }


        public void OnNotification(object emitter, MonItemNotificationArgs items)
        {
            if (!variablesMap.ContainsKey(items.name)) return;
    
            foreach (var itm in items.values)
            {
                if (itm.Value.GetType() == typeof(String)) continue;
                double value = extractMetricValue(itm);
                setMetric(items.name, value );
            }


        }

        public double extractMetricValue(DataValue itm)
        {
            double value = _config.failDefaultValue;
            if (itm.Value.GetType() == typeof(Boolean))
            {
                if ((bool)itm.Value) value = 1.0;
                else value = 0.0;
            }
            else
            {
                try
                {
                    value = (double)Convert.ChangeType(itm.Value, typeof(double));
                }
                catch
                {
                    value = _config.failDefaultValue;
                }
            }
            if (DataValue.IsBad(itm)) value = _config.failDefaultValue;
            return value;
        }

        public void setServiceManager(serviceManager serv)
        {
            _serv = serv;
        }
    }

    public class opcPromConfigWrapper
    {
        public opcPromConfig prometheus { get; set; }
        public opcPromConfigWrapper()
        {
            prometheus = new opcPromConfig();
        }
    }

    public class opcPromConfig
    {
        public int port { get; set; }
        public string systemLabelValue { get; set; }
        public string systemLabelName { get; set; }
        public double failDefaultValue { get; set; }
        public nodesConfig variableFilter{get; set;}
        public opcPromConfig()
        {
            port = 9988;
            systemLabelValue = "";
            systemLabelName = "";
            failDefaultValue = -999;
            variableFilter = new nodesConfig();
        }
    }
}
