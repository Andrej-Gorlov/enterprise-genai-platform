import { config } from '../config'

export interface Chat {
  id: string
  title: string
  createdAt: string
}

export async function getChats(): Promise<Chat[]> {
  const response = await fetch(`${config.apiBaseUrl}/chats`)

  if (!response.ok) {
    throw new Error(`Failed to load chats: ${response.status}`)
  }

  return response.json() as Promise<Chat[]>
}