import styled from '@emotion/styled'

export const Backdrop = styled.main`
  min-height: 100dvh;
  display: grid;
  place-items: center;
  padding: 24px 16px;
  background: linear-gradient(135deg, var(--brand-navy) 0%, var(--brand-teal) 100%);

  @media (max-width: 480px) {
    padding: 0;
    background: var(--color-bg-surface-login);
  }
`

export const Card = styled.section`
  width: 100%;
  --card-padding-x: 36px;
  --color-primary: var(--color-login-primary);
  --color-primary-active: var(--color-login-primary-active);
  --color-primary-bg: var(--color-login-primary-bg);
  --color-primary-contrast: var(--color-login-primary-contrast);
  --color-text-primary: var(--color-login-text-primary);
  --color-text-secondary: var(--color-login-text-secondary);
  max-width: 420px;
  padding: 40px var(--card-padding-x);
  border-radius: 20px;
  background: var(--color-bg-surface-login);
  box-shadow: 0 20px 60px var(--color-shadow);

  & .MuiFilledInput-root {
    background-color: var(--color-login-field-bg);
    color: var(--color-login-text-primary);

    &:hover,
    &.Mui-focused {
      background-color: var(--color-login-field-bg);
    }

    &::after {
      border-bottom-color: var(--color-login-field-accent);
    }
  }

  & .MuiFilledInput-input {
    color: var(--color-login-text-primary);

    &:-webkit-autofill {
      box-shadow: 0 0 0 100px var(--color-login-field-bg) inset;
      -webkit-text-fill-color: var(--color-login-text-primary);
      caret-color: var(--color-login-text-primary);
    }
  }

  & .MuiInputLabel-root {
    color: var(--color-login-text-secondary);

    &.Mui-focused {
      color: var(--color-login-field-accent);
    }
  }

  @media (max-width: 480px) {
    min-height: 100dvh;
    display: flex;
    flex-direction: column;
    justify-content: center;
    --card-padding-x: 24px;
    max-width: none;
    padding: 32px var(--card-padding-x);
    border-radius: 0;
    box-shadow: none;
  }
`

export const Brand = styled.header`
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  margin-bottom: 32px;
  text-align: center;
`

export const Logo = styled.img`
  display: block;
  width: 200px;
  max-width: 90%;
  height: auto;
  object-fit: contain;
  background: transparent;
  user-select: none;
  -webkit-user-drag: none;
`
