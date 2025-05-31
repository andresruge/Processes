// API helper for Processes.Frontend
// Uses VITE_API_URL from environment variables

export interface Process {
  id: string;
  name: string;
  status: string;
  createdAt: string;
  updatedAt: string;
  itemsToProcess: number;
  subprocesses: { [key: string]: string }; // This is a simplified representation, actual subprocess objects will be fetched separately
  processType: number;
  hangfireJobId?: string;
  errorMessage?: string;
}

export interface Subprocess {
  id: string;
  parentProcessId: string;
  status: string;
  createdAt: string;
  updatedAt: string;
  steps: { [key: string]: StepInfo };
  errorMessage?: string;
}

export interface StepInfo {
  status: string;
  description: string;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
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
  if (!res.ok) {
    const errorData = await res.json();
    throw new Error(errorData.title || "Failed to create process");
  }
  return res.json();
}

/**
 * Fetch a single process by ID from the API.
 * @param id The ID of the process to fetch.
 * @returns Promise<Process>
 */
export async function fetchProcessById(id: string): Promise<Process> {
  const res = await fetch(`${import.meta.env.VITE_API_URL}/processes/${id}`);
  if (!res.ok) {
    if (res.status === 404) throw new Error("Process not found");
    throw new Error("Failed to fetch process details");
  }
  return res.json();
}

/**
 * Fetch subprocesses for a given parent process ID.
 * @param parentId The ID of the parent process.
 * @returns Promise<Subprocess[]>
 */
export async function fetchSubprocessesByParentId(
  parentId: string
): Promise<Subprocess[]> {
  const res = await fetch(
    `${import.meta.env.VITE_API_URL}/processes/${parentId}/subprocesses`
  );
  if (!res.ok) throw new Error("Failed to fetch subprocesses");
  return res.json();
}

/**
 * Fetch a single subprocess by ID.
 * @param id The ID of the subprocess to fetch.
 * @returns Promise<Subprocess>
 */
export async function fetchSubprocessById(id: string): Promise<Subprocess> {
  const res = await fetch(`${import.meta.env.VITE_API_URL}/subprocesses/${id}`);
  if (!res.ok) {
    if (res.status === 404) throw new Error("Subprocess not found");
    throw new Error("Failed to fetch subprocess details");
  }
  return res.json();
}

/**
 * Start a process via the API.
 * @param id The ID of the process to start.
 * @returns Promise<void>
 */
export async function startProcess(id: string): Promise<void> {
  const res = await fetch(
    `${import.meta.env.VITE_API_URL}/processes/${id}/start`,
    {
      method: "POST",
    }
  );
  if (!res.ok) {
    const errorData = await res.json();
    throw new Error(errorData.title || "Failed to start process");
  }
}

/**
 * Cancel a process via the API.
 * @param id The ID of the process to cancel.
 * @returns Promise<void>
 */
export async function cancelProcess(id: string): Promise<void> {
  const res = await fetch(
    `${import.meta.env.VITE_API_URL}/processes/${id}/cancel`,
    {
      method: "POST",
    }
  );
  if (!res.ok) {
    const errorData = await res.json();
    throw new Error(errorData.title || "Failed to cancel process");
  }
}

/**
 * Revert a process via the API.
 * @param id The ID of the process to revert.
 * @returns Promise<void>
 */
export async function revertProcess(id: string): Promise<void> {
  const res = await fetch(
    `${import.meta.env.VITE_API_URL}/processes/${id}/revert`,
    {
      method: "POST",
    }
  );
  if (!res.ok) {
    const errorData = await res.json();
    throw new Error(errorData.title || "Failed to revert process");
  }
}

/**
 * Resume a process via the API.
 * @param id The ID of the process to resume.
 * @returns Promise<void>
 */
export async function resumeProcess(id: string): Promise<void> {
  const res = await fetch(
    `${import.meta.env.VITE_API_URL}/processes/${id}/resume`,
    {
      method: "POST",
    }
  );
  if (!res.ok) {
    const errorData = await res.json();
    throw new Error(errorData.title || "Failed to resume process");
  }
}
