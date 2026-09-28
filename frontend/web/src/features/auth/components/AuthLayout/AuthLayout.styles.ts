import styled from '@emotion/styled'

export const Backdrop = styled.main`
  min-height: 100dvh;
  display: grid;
  place-items: center;
  padding: 24px 16px;
  background: linear-gradient(135deg, var(--brand-navy) 0%, var(--brand-teal) 100%);

  @media (max-width: 480px) {
    padding: 0;
    background: var(--color-bg-surface);
  }
`

export const Card = styled.section`
  width: 100%;
  --card-padding-x: 36px;
  max-width: 420px;
  padding: 40px var(--card-padding-x);
  border-radius: 20px;
  background: var(--color-bg-surface);
  box-shadow: 0 20px 60px var(--color-shadow);

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
  width: 180px;
  height: auto;
`
