import { useState, useEffect, useCallback, useMemo } from 'react';
import type { MonitoredNode, MetricUpdatePayload, IncidentAlertPayload, NodeRecoveredPayload } from './types';
import { useSignalR } from './hooks/useSignalR';
import { Header } from './components/Header';
import { NodeCard } from './components/NodeCard';
import { IncidentBanner } from './components/IncidentBanner';
import { IncidentModal } from './components/IncidentModal';
import { Activity, Layers, Server, Cpu } from 'lucide-react';

const INITIAL_NODES: MonitoredNode[] = [
  {
    id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
    name: 'Payment Gateway API',
    targetUrl: 'https://api.payments.internal/v1/charge',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 24,
  },
  {
    id: 'a12bc34d-58ee-4172-b567-1e02b2c3d480',
    name: 'Billing & Invoicing Service',
    targetUrl: 'https://billing.internal/health',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 38,
  },
  {
    id: 'c98de76f-12aa-4982-c567-2e02b2c3d481',
    name: 'Auth & Identity Cluster',
    targetUrl: 'https://auth.internal/oauth/health',
    checkIntervalSeconds: 5,
    timeoutSeconds: 5,
    healthStatus: 'Healthy',
    lastCheckedAt: new Date().toISOString(),
    isActive: true,
    lastLatencyMs: 19,
  },
];

export function App() {
  const [nodes, setNodes] = useState<MonitoredNode[]>(INITIAL_NODES);
  const [incidents, setIncidents] = useState<Record<string, IncidentAlertPayload>>({});
  const [modalNode, setModalNode] = useState<MonitoredNode | null>(null);

  // 1. Попытка загрузить актуальные ноды из бэкенда
  useEffect(() => {
    fetch('http://localhost:5199/api/nodes')
      .then((res) => (res.ok ? res.json() : null))
      .then((data: MonitoredNode[] | null) => {
        if (data && data.length > 0) {
          setNodes(data);
        }
      })
      .catch(() => {
        // Если бэкенд оффлайн — используются встроенные мок-ноды
      });
  }, []);

  // 2. Обработка живой телеметрии из SignalR
  const handleMetricUpdated = useCallback((data: MetricUpdatePayload) => {
    setNodes((prev) =>
      prev.map((node) => {
        if (node.id === data.nodeId) {
          return {
            ...node,
            healthStatus: data.isSuccess ? 'Healthy' : 'Down',
            lastLatencyMs: data.responseTimeMs,
            lastCheckedAt: data.timestamp,
          };
        }
        return node;
      })
    );
  }, []);

  // 3. Обработка входящего инцидента с детальным анализом
  const handleIncidentAlert = useCallback((data: IncidentAlertPayload) => {
    setIncidents((prev) => ({
      ...prev,
      [data.incident.nodeId]: data,
    }));

    setNodes((prev) =>
      prev.map((node) => {
        if (node.id === data.incident.nodeId) {
          return {
            ...node,
            healthStatus: 'Down',
            lastCheckedAt: data.timestamp,
          };
        }
        return node;
      })
    );
  }, []);

  // 4. Обработка восстановления ноды
  const handleNodeRecovered = useCallback((data: NodeRecoveredPayload) => {
    setIncidents((prev) => {
      const next = { ...prev };
      delete next[data.nodeId];
      return next;
    });

    setNodes((prev) =>
      prev.map((node) => {
        if (node.id === data.nodeId) {
          return {
            ...node,
            healthStatus: 'Healthy',
            lastCheckedAt: data.timestamp,
          };
        }
        return node;
      })
    );

    setModalNode((curr) => (curr?.id === data.nodeId ? null : curr));
  }, []);

  // 5. Подключение хука SignalR
  const { isConnected } = useSignalR(handleMetricUpdated, handleIncidentAlert, handleNodeRecovered);

  // 6. Симуляция сбоя по клику на кнопку
  const handleSimulateFailure = async (nodeId: string) => {
    const targetNode = nodes.find((n) => n.id === nodeId);
    if (!targetNode) return;

    try {
      const res = await fetch(`http://localhost:5199/api/nodes/${nodeId}/simulate-failure`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          errorMessage: `HTTP 504 Gateway Timeout: Upstream dependency connection aborted after 30000ms`,
          failureType: 'HttpTimeout',
          statusCode: 504,
        }),
      });

      if (!res.ok) throw new Error('API request failed');
    } catch {
      // Изолированный fallback для мгновенного UI-отклика
      handleIncidentAlert({
        incident: {
          incidentId: 'inc-' + Date.now(),
          nodeId: targetNode.id,
          nodeName: targetNode.name,
          targetUrl: targetNode.targetUrl,
          errorMessage: 'HTTP 504 Gateway Timeout: Upstream dependency connection aborted after 30000ms',
          failureType: 'HttpTimeout',
          statusCode: 504,
          occurredAt: new Date().toISOString(),
        },
        aiDiagnosis: {
          rootCause: `The ${targetNode.name} failed to complete its upstream database transaction within the 30-second timeout window due to connection pool saturation or a deadlocked query.`,
          immediateFix: 'Restart or horizontally scale backend instances and cycle database connection pools.',
          prevention: 'Apply circuit breaker with Polly v8 and configure bulkheads to isolate slow upstream transactions.',
        },
        timestamp: new Date().toISOString(),
      });
    }
  };

  // 7. Восстановление конкретной ноды
  const handleRestoreNode = (nodeId: string) => {
    setNodes((prev) =>
      prev.map((node) => {
        if (node.id === nodeId) {
          return {
            ...node,
            healthStatus: 'Healthy',
            lastLatencyMs: Math.floor(Math.random() * 20) + 15,
            lastCheckedAt: new Date().toISOString(),
          };
        }
        return node;
      })
    );

    setIncidents((prev) => {
      const next = { ...prev };
      delete next[nodeId];
      return next;
    });

    if (modalNode?.id === nodeId) {
      setModalNode(null);
    }
  };

  // 8. Восстановление всех упавших нод сразу
  const handleResolveAll = () => {
    setNodes((prev) =>
      prev.map((n) => ({
        ...n,
        healthStatus: 'Healthy',
        lastLatencyMs: n.healthStatus === 'Down' ? Math.floor(Math.random() * 20) + 15 : n.lastLatencyMs,
        lastCheckedAt: new Date().toISOString(),
      }))
    );
    setIncidents({});
    setModalNode(null);
  };

  const healthyNodesCount = nodes.filter((n) => n.healthStatus === 'Healthy').length;
  const healthPercent = Math.round((healthyNodesCount / nodes.length) * 100);
  const activeDownCount = nodes.filter((n) => n.healthStatus === 'Down').length;

  // Самый свежий инцидент для верхнего баннера
  const latestIncident = useMemo(() => {
    const list = Object.values(incidents);
    return list.length > 0 ? list[list.length - 1] : null;
  }, [incidents]);

  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-100 flex flex-col font-sans selection:bg-zinc-800 selection:text-zinc-100">
      {/* Шапка дашборда */}
      <Header isConnected={isConnected} activeIncidentsCount={activeDownCount} />

      {/* Основной контент */}
      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* Баннер активного инцидента */}
        <IncidentBanner
          incidentAlert={latestIncident}
          activeCount={activeDownCount}
          onResolve={handleResolveAll}
          onViewModal={() => {
            const downNode = nodes.find((n) => n.id === latestIncident?.incident.nodeId);
            if (downNode) setModalNode(downNode);
          }}
        />

        {/* Сводная панель метрик системы */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
          <div className="p-4 rounded-lg bg-zinc-900 border border-zinc-800">
            <div className="flex items-center gap-2 text-zinc-400 text-xs mb-1 font-mono">
              <Server className="w-3.5 h-3.5 text-zinc-400" />
              <span>Monitored Nodes</span>
            </div>
            <div className="text-2xl font-bold font-mono text-zinc-100">{nodes.length}</div>
          </div>

          <div className="p-4 rounded-lg bg-zinc-900 border border-zinc-800">
            <div className="flex items-center gap-2 text-zinc-400 text-xs mb-1 font-mono">
              <Activity className="w-3.5 h-3.5 text-emerald-400" />
              <span>Cluster Health</span>
            </div>
            <div
              className={`text-2xl font-bold font-mono ${
                healthPercent === 100
                  ? 'text-emerald-400'
                  : healthPercent > 50
                  ? 'text-amber-400'
                  : 'text-red-400'
              }`}
            >
              {healthPercent}%
            </div>
          </div>

          <div className="p-4 rounded-lg bg-zinc-900 border border-zinc-800">
            <div className="flex items-center gap-2 text-zinc-400 text-xs mb-1 font-mono">
              <Layers className="w-3.5 h-3.5 text-zinc-400" />
              <span>Resilience Engine</span>
            </div>
            <div className="text-sm font-semibold text-zinc-200 mt-1 font-mono">Polly v8 + Redis</div>
          </div>

          <div className="p-4 rounded-lg bg-zinc-900 border border-zinc-800">
            <div className="flex items-center gap-2 text-zinc-400 text-xs mb-1 font-mono">
              <Cpu className="w-3.5 h-3.5 text-zinc-400" />
              <span>Diagnostic Engine</span>
            </div>
            <div className="text-sm font-semibold text-zinc-200 mt-1 font-mono">Autonomous RCA</div>
          </div>
        </div>

        {/* Сетка карточек микросервисов */}
        <div className="mb-6 flex items-center justify-between">
          <div>
            <h2 className="text-base font-semibold text-zinc-100 font-mono">
              Live Microservices Telemetry
            </h2>
            <p className="text-xs text-zinc-400 font-mono mt-0.5">
              Live WebSocket metrics stream updated every 5 seconds
            </p>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {nodes.map((node) => (
            <NodeCard
              key={node.id}
              node={node}
              onSimulateFailure={handleSimulateFailure}
              onRestoreNode={handleRestoreNode}
              onViewAnalysis={(target) => setModalNode(target)}
            />
          ))}
        </div>
      </main>

      {/* Модальное окно детального разбора инцидента */}
      <IncidentModal
        node={modalNode}
        diagnosis={modalNode ? incidents[modalNode.id]?.aiDiagnosis ?? null : null}
        isOpen={modalNode !== null}
        onClose={() => setModalNode(null)}
        onRestore={handleRestoreNode}
      />

      {/* Подвал без личных имен и лишнего шума */}
      <footer className="border-t border-zinc-900 py-6 text-center text-xs font-mono text-zinc-400">
        NexusPulse v2.0 • Distributed Observability &amp; Telemetry Platform
      </footer>
    </div>
  );
}

export default App;
