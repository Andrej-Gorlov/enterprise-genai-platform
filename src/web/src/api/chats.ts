import { config } from '../config'

export interface Chat {
  id: string
  title: string
  createdAt: string
}

export interface CreateChatRequest {
  title: string
}

export async function getChats(): Promise<Chat[]> {
  const response = await fetch(`${config.apiBaseUrl}/chats`)

  if (!response.ok) {
    throw new Error(`Failed to load chats: ${response.status}`)
  }

  return response.json() as Promise<Chat[]>
}

export async function createChat(request: CreateChatRequest): Promise<Chat> {
  const response = await fetch(`${config.apiBaseUrl}/chats`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })

  if (!response.ok) {
    throw new Error(`Failed to create chat: ${response.status}`)
  }

  return response.json() as Promise<Chat>
}