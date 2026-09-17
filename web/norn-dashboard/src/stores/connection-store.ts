import { create } from "zustand";

export type ConnectionStatus = "Connecting" | "Connected" | "Reconnecting" | "Disconnected";

interface ConnectionState {
  status: ConnectionStatus;
  setStatus: (status: ConnectionStatus) => void;
}

export const useConnectionStore = create<ConnectionState>((set) => ({
  status: "Connecting",
  setStatus: (status) => set({ status }),
}));
