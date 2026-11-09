import styled from '@emotion/styled'
import { MutedText, SectionTitle } from '../../styles/components/texts'

export const LandingSectionHead = styled.div`
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
  max-width: 720px;
  margin: 0 auto 56px;
  text-align: center;
`

export const LandingSectionTitle = styled(SectionTitle)`
  max-width: 720px;
  margin: 0;
  font-size: clamp(32px, 4.5vw, 48px);
  font-weight: 700;
  line-height: 1.15;
  letter-spacing: -0.03em;
  text-align: center;
  color: var(--color-text-primary);

  @media (max-width: 600px) {
    font-size: 26px;
  }
`

export const LandingLead = styled(MutedText)`
  max-width: 680px;
  margin: 12px auto 0;
  text-align: center;
  font-size: 18px;
  line-height: 1.6;
  color: var(--color-text-secondary);
  opacity: 0.9;

  @media (max-width: 600px) {
    font-size: 15px;
  }
`
