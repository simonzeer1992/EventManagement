export type EventItem = {
  id: string; title: string; description: string; date: string;
  maxCapacity: number; registrationCount: number;
};
export type EventInput = Omit<EventItem, 'id' | 'registrationCount'>;

export async function request<T = void>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const response = await fetch(`/api/events${path}`, {
    method,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body)
  });
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    const errors = problem?.errors ? Object.values(problem.errors).flat().join(' ') : '';
    throw new Error(problem?.detail || errors || problem?.title || 'The request failed. Please try again.');
  }
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
