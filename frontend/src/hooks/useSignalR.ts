import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import type { MetricUpdatePayload, IncidentAlertPayload, NodeRecoveredPayload } from '../types';

export function useSignalR(
  onMetricUpdated?: (data: MetricUpdatePayload) => void,
  onIncidentAlert?: (data: IncidentAlertPayload) => void,
  onNodeRecovered?: (data: NodeRecoveredPayload) => void
) {
  const [isConnected, setIsConnected] = useState<boolean>(false);

  // Храним актуальные ссылки на коллбеки, чтобы не пересоздавать WebSocket-соединение
  const onMetricUpdatedRef = useRef(onMetricUpdated);
  const onIncidentAlertRef = useRef(onIncidentAlert);
  const onNodeRecoveredRef = useRef(onNodeRecovered);

  useEffect(() => {
    onMetricUpdatedRef.current = onMetricUpdated;
    onIncidentAlertRef.current = onIncidentAlert;
    onNodeRecoveredRef.current = onNodeRecovered;
  }, [onMetricUpdated, onIncidentAlert, onNodeRecovered]);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5199/hubs/metrics', {
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connection.on('MetricUpdated', (data: MetricUpdatePayload) => {
      onMetricUpdatedRef.current?.(data);
    });

    connection.on('IncidentAlert', (data: IncidentAlertPayload) => {
      onIncidentAlertRef.current?.(data);
    });

    connection.on('NodeRecovered', (data: NodeRecoveredPayload) => {
      onNodeRecoveredRef.current?.(data);
    });

    connection.onreconnecting(() => setIsConnected(false));
    connection.onreconnected(() => setIsConnected(true));
    connection.onclose(() => setIsConnected(false));

    connection
      .start()
      .then(() => setIsConnected(true))
      .catch((err) => {
        console.error('Ошибка подключения SignalR:', err);
        setIsConnected(false);
      });

    return () => {
      connection.stop();
    };
  }, []);

  return { isConnected };
}
