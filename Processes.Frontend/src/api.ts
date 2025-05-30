// API helper for Processes.Frontend
// Uses VITE_API_URL from environment variables

export interface Process {
  id: string;
  name: string;
  status: string;
  createdAt: string;
}

/**
 * Fetch all processes from the API.
 * @returns Promise<Process[]>
 */
export async function fetchProcesses(): Promise<Process[]> {
  const res = await fetch(`${import.meta.env.VITE_API_URL}/processes`);
  if (!res.ok) throw new Error("Failed to fetch processes");
  return res.json();
}

/**
 * Create a new process via the API.
 * @param name Name of the process
 * @param numberOfSubprocesses Number of subprocesses
 * @param processType Process type (enum value)
 * @returns Promise<Process>
 */
export async function createProcess(
  name: string,
  numberOfSubprocesses: number,
  processType: number
): Promise<Process> {
  const res = await fetch(`${import.meta.env.VITE_API_URL}/processes`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      name,
      numberOfSubprocesses,
      processType,
    }),
  });
  if (!res.ok) throw new Error("Failed to create process");
  return res.json();
}
