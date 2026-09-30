import { useState } from 'react'
import { getChats, type Chat } from './api/chats'
import { config } from './config'

function App() {
  const [chats, setChats] = useState<Chat[]>([])
  const [error, setError] = useState<string | null>(null)

  async function handleLoadChats() {
    try {
      setError(null)
      setChats(await getChats())
    } catch {
      setError('Failed to load chats')
    }
  }

  return (
    <main>
      <h1>Enterprise GenAI Platform</h1>
      <p>Full Stack MVP</p>
      <p>Core API: {config.apiBaseUrl}</p>

      <button onClick={handleLoadChats}>Load chats</button>

      {error && <p>{error}</p>}

      <ul>
        {chats.map((chat) => (
          <li key={chat.id}>{chat.title}</li>
        ))}
      </ul>
    </main>
  )
}

export default App