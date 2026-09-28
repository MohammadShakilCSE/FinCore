export type User = { id: string; name: string; email: string };
export type Session = { accessToken: string; expiresAt: number; user: User };

export type LoginInput = { email: string; password: string };
export type RegisterInput = LoginInput & { name: string };
