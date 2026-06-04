package com.java.aistudyhubbe.repository;

import com.java.aistudyhubbe.entity.RefreshToken;
import com.java.aistudyhubbe.entity.User;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.stereotype.Repository;

import java.util.Optional;
import java.util.UUID;

@Repository
public interface RefreshTokenRepository extends JpaRepository<RefreshToken, UUID> {
    Optional<RefreshToken> findByToken(String token);

    Optional<RefreshToken> findByUser(User user);

    @Modifying(flushAutomatically = true)
    int deleteByUser(User user);
}
