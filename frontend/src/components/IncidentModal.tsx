import React from 'react';
import type { MonitoredNode, AiDiagnosis } from '../types';
import { AlertTriangle, X, RotateCcw, Terminal, ExternalLink, ShieldAlert, Cpu, Wrench } from 'lucide-react';

interface IncidentModalProps {
  node: MonitoredNode | null;
  diagnosis: AiDiagnosis | null;
  isOpen: boolean;
  onClose: () => void;
  onRestore: (nodeId: string) => void;
}

export const IncidentModal: React.FC<IncidentModalProps> = ({
  node,
  diagnosis,
  isOpen,
  onClose,
  onRestore,
}) => {
  if (!isOpen || !node) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4 overflow-y-auto animate-in fade-in duration-150">
      <div className="relative w-full max-w-2xl rounded-xl border border-zinc-700 bg-zinc-900 shadow-2xl p-6 my-8 font-sans">
        {/* Close Button */}
        <button
          type="button"
          onClick={onClose}
          className="absolute top-5 right-5 p-1.5 rounded-md text-zinc-400 hover:text-zinc-100 hover:bg-zinc-800 transition"
        >
          <X className="w-5 h-5" />
        </button>

        {/* Header */}
        <div className="flex items-start gap-4 mb-6">
          <div className="w-10 h-10 rounded-md bg-red-950/60 border border-red-800/80 text-red-400 flex items-center justify-center flex-shrink-0">
            <AlertTriangle className="w-5 h-5" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="text-[10px] font-mono uppercase tracking-wider text-red-400 bg-red-950/50 px-2 py-0.5 rounded border border-red-800/60 font-semibold">
                Critical Service Failure
              </span>
              <span className="text-xs font-mono text-zinc-400">
                HTTP 504 • Polly Circuit Breaker: OPEN
              </span>
            </div>
            <h2 className="text-lg font-semibold text-zinc-100 mt-1 font-mono">
              {node.name} Outage &amp; Postmortem
            </h2>
            <a
              href={node.targetUrl}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 text-xs text-zinc-400 hover:text-zinc-200 transition font-mono mt-0.5"
            >
              <span>{node.targetUrl}</span>
              <ExternalLink className="w-3 h-3" />
            </a>
          </div>
        </div>

        {/* RCA Section */}
        <div className="rounded-lg border border-zinc-800 bg-zinc-950 p-4 mb-5">
          <div className="flex items-center gap-2 mb-3 text-zinc-300 font-mono text-xs tracking-wider uppercase font-semibold border-b border-zinc-800/80 pb-2">
            <Cpu className="w-4 h-4 text-zinc-400" />
            <span>Root Cause Analysis &amp; Diagnostic Breakdown</span>
          </div>

          <div className="space-y-3 text-xs">
            {/* 1. Root Cause */}
            <div className="p-3 rounded-md bg-zinc-900 border border-zinc-800">
              <div className="flex items-center gap-1.5 text-red-400 text-[11px] font-mono uppercase tracking-wider font-semibold mb-1">
                <ShieldAlert className="w-3.5 h-3.5" />
                <span>Root Cause Hypothesis</span>
              </div>
              <p className="text-zinc-300 font-sans text-xs leading-relaxed">
                {diagnosis?.rootCause ??
                  (node.name.includes('Payment')
                    ? 'Upstream banking acquiring gateway timed out after 30000ms during credit card tokenization due to acquiring bank thread-pool exhaustion.'
                    : node.name.includes('Billing')
                    ? 'PostgreSQL transaction deadlock detected during recurring subscription invoice generation under heavy row lock contention.'
                    : 'OAuth2 / JWT token validation cluster exhausted available HTTP connection sockets due to sudden authentication spike.')}
              </p>
            </div>

            {/* 2. Immediate Fix */}
            <div className="p-3 rounded-md bg-zinc-900 border border-zinc-800">
              <div className="flex items-center gap-1.5 text-emerald-400 text-[11px] font-mono uppercase tracking-wider font-semibold mb-1">
                <Wrench className="w-3.5 h-3.5" />
                <span>Immediate Mitigation</span>
              </div>
              <p className="text-zinc-300 font-sans text-xs leading-relaxed">
                {diagnosis?.immediateFix ??
                  'Cycle container instances, kill long-running blocked transactions, and verify network connectivity.'}
              </p>
            </div>

            {/* 3. Prevention */}
            <div className="p-3 rounded-md bg-zinc-900 border border-zinc-800">
              <div className="flex items-center gap-1.5 text-zinc-400 text-[11px] font-mono uppercase tracking-wider font-semibold mb-1">
                <Cpu className="w-3.5 h-3.5" />
                <span>Preventative Architecture</span>
              </div>
              <p className="text-zinc-300 font-sans text-xs leading-relaxed">
                {diagnosis?.prevention ??
                  'Implement Keyed Polly v8 Circuit Breaker policies, add query timeouts, and scale read-replicas.'}
              </p>
            </div>
          </div>
        </div>

        {/* Telemetry Trace Box */}
        <div className="rounded-md bg-zinc-950 border border-zinc-800/80 p-3 mb-6 font-mono text-[11px] text-zinc-400">
          <div className="flex items-center gap-2 text-zinc-500 mb-1.5 text-[10px] uppercase font-semibold">
            <Terminal className="w-3.5 h-3.5" />
            <span>Telemetry Diagnostic Trace</span>
          </div>
          <div className="text-red-400 font-mono">
            System.Net.Http.HttpRequestException: Response status code does not indicate success: 504 (Gateway Timeout)
          </div>
          <div className="text-zinc-500 mt-1">
            &nbsp;&nbsp;&nbsp;at Polly.ResiliencePipeline.ExecuteAsync(...) in ResilienceEngine.cs:line 84
          </div>
          <div className="text-zinc-500">
            &nbsp;&nbsp;&nbsp;at NexusPulse.NodeHealthMonitoringWorker.ProbeHostAsync(...) in Worker.cs:line 97
          </div>
        </div>

        {/* Action Buttons */}
        <div className="flex items-center justify-end gap-3 pt-3 border-t border-zinc-800">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 rounded-md text-xs font-mono font-medium text-zinc-400 hover:text-zinc-200 hover:bg-zinc-800 transition"
          >
            Close
          </button>
          <button
            type="button"
            onClick={() => {
              onRestore(node.id);
              onClose();
            }}
            className="inline-flex items-center gap-2 px-4 py-2 rounded-md text-xs font-mono font-medium bg-emerald-800/80 hover:bg-emerald-700 text-emerald-100 border border-emerald-700/60 shadow-sm transition"
          >
            <RotateCcw className="w-3.5 h-3.5" />
            Restore Node &amp; Bring Online
          </button>
        </div>
      </div>
    </div>
  );
};
