from sentence_transformers import SentenceTransformer


model = SentenceTransformer(
    "sentence-transformers/all-MiniLM-L6-v2"
)

texts = [
    "The application stores data in PostgreSQL.",
    "The system persists information in a PostgreSQL database.",
    "A cat is sleeping on the sofa.",
]

embeddings = model.encode(texts)

similarities = model.similarity(
    embeddings,
    embeddings,
)

sim_1_2 = similarities[0][1].item()
sim_1_3 = similarities[0][2].item()

print("Embedding dimension:", embeddings.shape[1])
print(f"Similarity (text 1, text 2): {sim_1_2:.4f}")
print(f"Similarity (text 1, text 3): {sim_1_3:.4f}")

if sim_1_2 > sim_1_3:
    print("Success: related > unrelated")