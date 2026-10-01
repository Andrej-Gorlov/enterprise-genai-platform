import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { createChat, getChats } from "./api/chats";
import App from "./App";

vi.mock("./config", () => ({
  config: {
    apiBaseUrl: "http://test-api",
  },
}));

vi.mock("./api/chats", () => ({
  getChats: vi.fn(),
  createChat: vi.fn(),
}));

const mockedGetChats = vi.mocked(getChats);
const mockedCreateChat = vi.mocked(createChat);

function renderApp() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
      mutations: {
        retry: false,
      },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>,
  );
}

describe("App", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("loads and displays chats", async () => {
    // Arrange
    mockedGetChats.mockResolvedValue([
      {
        id: "1",
        title: "Existing chat",
        createdAt: "2026-10-01T12:00:00Z",
      },
    ]);

    // Act
    renderApp();

    // Assert
    expect(await screen.findByText("Existing chat")).toBeInTheDocument();

    expect(mockedGetChats).toHaveBeenCalledTimes(1);
  });

  it("creates a chat and reloads the chat list", async () => {
    // Arrange
    const existingChat = {
      id: "1",
      title: "Existing chat",
      createdAt: "2026-10-01T12:00:00Z",
    };

    const createdChat = {
      id: "2",
      title: "Created from test",
      createdAt: "2026-10-01T12:01:00Z",
    };

    mockedGetChats
      .mockResolvedValueOnce([existingChat])
      .mockResolvedValueOnce([existingChat, createdChat]);

    mockedCreateChat.mockResolvedValue(createdChat);

    const user = userEvent.setup();

    renderApp();

    await screen.findByText("Existing chat");

    // Act
    await user.type(
      screen.getByPlaceholderText("Chat title"),
      "Created from test",
    );

    await user.click(
      screen.getByRole("button", {
        name: "Create chat",
      }),
    );

    // Assert
    expect(await screen.findByText("Created from test")).toBeInTheDocument();

    expect(mockedCreateChat).toHaveBeenCalledTimes(1);

    expect(mockedCreateChat.mock.calls[0][0]).toEqual({
      title: "Created from test",
    });

    expect(mockedGetChats).toHaveBeenCalledTimes(2);
  });
});
