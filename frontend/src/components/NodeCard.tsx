import React from 'react';
import type { MonitoredNode } from '../types';
import { Server, ExternalLink, RotateCcw, FileText, AlertCircle, CheckCircle2 } from 'lucide-react';

export interface NodeCardProps {
  node: MonitoredNode;
  onSimulateFailure: (id: string) => void;
  onRestoreNode: (id: string) => void;
  onViewAnalysis: (node: MonitoredNode) => void;
}

export const NodeCard: React.FC<NodeCardProps> = ({
  node,
  onSimulateFailure,
  onRestoreNode,
  onViewAnalysis,
}) => {
  const status = node.healthStatus || 'Healthy';
  const isDown = status === 'Down';
  const isDegraded = status === 'Degraded';
  const latency = node.lastLatencyMs;

  return (
    <div
      className={`flex flex-col justify-between rounded-lg border p-5 transition-all duration-150 ${
        isDown
          ? 'bg-zinc-900/95 border-red-500/60 shadow-sm shadow-red-950/20'
          : isDegraded
          ? 'bg-zinc-900/80 border-amber-500/60'
          : 'bg-zinc-900/50 border-zinc-800 hover:border-zinc-700'
      }`}
    >
      <div>
        {/* Top: Name, Target URL & Status Badge */}
        <div className="flex items-start justify-between gap-3 mb-4">
          <div className="flex items-center gap-3 min-w-0">
            <div
              className={`w-9 h-9 rounded-md flex items-center justify-center border flex-shrink-0 ${
                isDown
                  ? 'bg-red-950/40 border-red-800/60 text-red-400'
                  : 'bg-zinc-950 border-zinc-800 text-zinc-300'
              }`}
            >
              <Server className="w-4 h-4" />
            </div>
            <div className="min-w-0 flex-1">
              <h3 className="font-medium text-zinc-100 text-sm truncate font-mono">
                {node.name}
              </h3>
              <a
                href={node.targetUrl}
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-1 text-[11px] text-zinc-400 hover:text-zinc-200 transition font-mono truncate max-w-full"
              >
                <span className="truncate">{node.targetUrl}</span>
                <ExternalLink className="w-2.5 h-2.5 flex-shrink-0" />
              </a>
            </div>
          </div>

          <button
            type="button"
            onClick={() => isDown && onViewAnalysis(node)}
            disabled={!isDown}
            title={isDown ? 'Click to inspect RCA diagnostics' : 'Node is healthy'}
            className={`inline-flex items-center gap-1.5 px-2 py-0.5 rounded text-[11px] font-mono border uppercase tracking-wider transition ${
              isDown
                ? 'bg-red-950/50 text-red-400 border-red-800/60 hover:bg-red-900/40 cursor-pointer'
                : isDegraded
                ? 'bg-amber-950/50 text-amber-400 border-amber-800/60'
                : 'bg-zinc-950 text-emerald-400 border-emerald-900/60'
            }`}
          >
            {isDown ? (
              <>
                <AlertCircle className="w-3 h-3 text-red-400 animate-pulse" />
                Down
              </>
            ) : (
              <>
                <CheckCircle2 className="w-3 h-3 text-emerald-400" />
                Healthy
              </>
            )}
          </button>
        </div>

        {/* Telemetry Metrics Grid */}
        <div
          onClick={() => isDown && onViewAnalysis(node)}
          className={`grid grid-cols-2 gap-2 rounded-md bg-zinc-950 border border-zinc-800/80 p-3 text-xs mb-4 ${
            isDown ? 'cursor-pointer hover:border-zinc-700 transition' : ''
          }`}
        >
          <div>
            <span className="text-[10px] font-mono uppercase text-zinc-400 block mb-0.5">
              Response Time
            </span>
            <span
              className={`font-mono text-xs font-semibold ${
                isDown ? 'text-red-400' : 'text-zinc-200'
              }`}
            >
              {isDown ? 'Timeout (504)' : `${latency ?? 24} ms`}
            </span>
          </div>
          <div>
            <span className="text-[10px] font-mono uppercase text-zinc-400 block mb-0.5">
              Probe Rate
            </span>
            <span className="font-mono text-xs text-zinc-200">
              {node.checkIntervalSeconds ?? 5}s interval
            </span>
          </div>
        </div>
      </div>

      {/* Action Controls */}
      {isDown ? (
        <div className="grid grid-cols-2 gap-2 pt-2 border-t border-zinc-800/60">
          <button
            type="button"
            onClick={() => onViewAnalysis(node)}
            className="flex items-center justify-center gap-1.5 rounded-md border border-zinc-700 bg-zinc-800 hover:bg-zinc-750 hover:border-zinc-600 px-3 py-2 text-xs font-mono text-zinc-200 transition"
          >
            <FileText className="w-3.5 h-3.5 text-zinc-400" />
            Inspect RCA
          </button>
          <button
            type="button"
            onClick={() => onRestoreNode(node.id)}
            className="flex items-center justify-center gap-1.5 rounded-md border border-emerald-800/60 bg-emerald-950/40 hover:bg-emerald-900/60 px-3 py-2 text-xs font-mono text-emerald-300 transition"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            Restore
          </button>
        </div>
      ) : (
        <button
          type="button"
          onClick={() => onSimulateFailure(node.id)}
          className="w-full flex items-center justify-center gap-1.5 rounded-md border border-zinc-800 bg-zinc-950 hover:bg-zinc-800/80 hover:border-zinc-700 px-3 py-2 text-xs font-mono text-zinc-300 transition"
        >
          Simulate Outage
        </button>
      )}
    </div>
  );
};
