import { config } from "./config";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createChat, getChats } from "./api/chats";

function App() {
  const chatsQuery = useQuery({
    queryKey: ["chats"],
    queryFn: getChats,
  });

  const queryClient = useQueryClient();
  const [title, setTitle] = useState("");

  const createChatMutation = useMutation({
    mutationFn: createChat,
    onSuccess: async () => {
      setTitle("");

      await queryClient.invalidateQueries({
        queryKey: ["chats"],
      });
    },
  });

  return (
    <main>
      <h1>Enterprise GenAI Platform</h1>
      <p>Full Stack MVP</p>
      <p>Core API: {config.apiBaseUrl}</p>

      <form
        onSubmit={(event) => {
          event.preventDefault();

          const trimmedTitle = title.trim();

          if (!trimmedTitle) {
            return;
          }

          createChatMutation.mutate({
            title: trimmedTitle,
          });
        }}
      >
        <input
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          placeholder="Chat title"
          maxLength={200}
        />

        <button type="submit" disabled={createChatMutation.isPending}>
          {createChatMutation.isPending ? "Creating..." : "Create chat"}
        </button>
      </form>

      {createChatMutation.isError && <p>Failed to create chat</p>}

      {chatsQuery.isPending && <p>Loading chats...</p>}
      {chatsQuery.isError && <p>Failed to load chats</p>}
      {chatsQuery.data && (
        <ul>
          {chatsQuery.data.map((chat) => (
            <li key={chat.id}>{chat.title}</li>
          ))}
        </ul>
      )}
    </main>
  );
}

export default App;
