import React from 'react';
import type { IncidentAlertPayload } from '../types';
import { AlertCircle, CheckCircle2, ChevronRight, FileText } from 'lucide-react';

interface IncidentBannerProps {
  incidentAlert: IncidentAlertPayload | null;
  onResolve: () => void;
  onViewModal?: () => void;
  activeCount?: number;
}

export const IncidentBanner: React.FC<IncidentBannerProps> = ({
  incidentAlert,
  onResolve,
  onViewModal,
  activeCount = 1,
}) => {
  if (!incidentAlert) return null;

  const { incident, aiDiagnosis } = incidentAlert;

  return (
    <div className="rounded-lg border border-red-500/60 bg-zinc-900/95 p-5 mb-8 shadow-sm">
      {/* Incident Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-zinc-800 pb-4">
        <div className="flex items-start sm:items-center gap-3">
          <div className="w-8 h-8 rounded-md bg-red-950/60 border border-red-800/80 flex items-center justify-center text-red-400 flex-shrink-0">
            <AlertCircle className="w-4 h-4" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <span className="text-[10px] font-mono uppercase tracking-wider text-red-400 bg-red-950/40 px-2 py-0.5 rounded border border-red-800/50">
                Critical Outage {activeCount > 1 ? `(${activeCount} Services Affected)` : ''}
              </span>
              <span className="text-xs font-mono text-zinc-400">
                {incident.failureType} • HTTP {incident.statusCode ?? 504}
              </span>
            </div>
            <h2 className="text-base font-semibold text-zinc-100 mt-1 font-mono">
              {incident.nodeName} is unreachable
            </h2>
            <p className="text-xs text-zinc-400 font-mono mt-0.5">{incident.errorMessage}</p>
          </div>
        </div>

        {/* Action Controls */}
        <div className="flex items-center gap-2 self-start sm:self-auto">
          {onViewModal && (
            <button
              type="button"
              onClick={onViewModal}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-mono text-zinc-200 bg-zinc-800 border border-zinc-700 hover:bg-zinc-750 transition"
            >
              <FileText className="w-3.5 h-3.5 text-zinc-400" />
              Inspect Postmortem
              <ChevronRight className="w-3 h-3 text-zinc-500" />
            </button>
          )}
          <button
            type="button"
            onClick={onResolve}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-mono text-emerald-300 bg-emerald-950/40 border border-emerald-800/60 hover:bg-emerald-900/40 transition"
          >
            <CheckCircle2 className="w-3.5 h-3.5" />
            {activeCount > 1 ? 'Resolve All' : 'Resolve Incident'}
          </button>
        </div>
      </div>

      {/* Structured Diagnostics Summary */}
      {aiDiagnosis && (
        <div className="mt-4 grid grid-cols-1 md:grid-cols-3 gap-3 text-xs font-mono">
          {/* Root Cause */}
          <div className="bg-zinc-950 p-3.5 rounded-md border border-zinc-800/80">
            <span className="text-red-400 uppercase tracking-wider text-[10px] font-semibold block mb-1.5">
              Root Cause Hypothesis
            </span>
            <p className="text-zinc-300 font-sans text-xs leading-relaxed">
              {aiDiagnosis.rootCause}
            </p>
          </div>

          {/* Immediate Remediation */}
          <div className="bg-zinc-950 p-3.5 rounded-md border border-zinc-800/80">
            <span className="text-emerald-400 uppercase tracking-wider text-[10px] font-semibold block mb-1.5">
              Immediate Mitigation
            </span>
            <p className="text-zinc-300 font-sans text-xs leading-relaxed">
              {aiDiagnosis.immediateFix}
            </p>
          </div>

          {/* Long-term Prevention */}
          <div className="bg-zinc-950 p-3.5 rounded-md border border-zinc-800/80">
            <span className="text-zinc-400 uppercase tracking-wider text-[10px] font-semibold block mb-1.5">
              Preventative Architecture
            </span>
            <p className="text-zinc-300 font-sans text-xs leading-relaxed">
              {aiDiagnosis.prevention}
            </p>
          </div>
        </div>
      )}
    </div>
  );
};
